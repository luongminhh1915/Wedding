$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../admin-vendor-qa-20261029/AdminVendorQa.Common.ps1')
$manifestPath='.deployment/private/admin-vendor-qa-20261029/794ea61e2c7e4538acda17021488247c/fixture.json'
$manifest=Get-QaManifest $manifestPath
Assert-QaConfiguration $manifest
$base='http://127.0.0.1:5029'
$checks=[Collections.Generic.List[object]]::new()
function Call([string]$method,[string]$path,$body=$null,[string]$token=''){
 $headers=@{};if($token){$headers.Authorization='Bearer '+$token}
 $args=@{Uri=$base+$path;Method=$method;Headers=$headers;SkipHttpErrorCheck=$true;TimeoutSec=20}
 if($null -ne $body){$args.ContentType='application/json';$args.Body=ConvertTo-Json -InputObject $body -Depth 15 -Compress}
 return Invoke-WebRequest @args
}
function Check([string]$name,[bool]$pass){$checks.Add([pscustomobject]@{name=$name;pass=$pass});if(-not $pass){throw "Owned QA check failed: $name"}}
function Login($email,$password){$r=Call 'POST' '/api/auth/login' @{email=$email;password=$password};if($r.StatusCode -ne 200){throw 'Synthetic login failed.'};return $r.Content|ConvertFrom-Json}
$admin=$manifest.seedActors|Where-Object role -eq SuperAdmin
$moderator=$manifest.seedActors|Where-Object role -eq Moderator
$customer=$manifest.seedActors|Where-Object role -eq Customer
$adminSession=Login $admin.email $admin.password
$modSession=Login $moderator.email $moderator.password
$customerSession=Login $customer.email $customer.password
Check 'Anonymous provisioning requires login' ((Call 'POST' '/api/users' @{}).StatusCode -eq 401)
Check 'Customer cannot provision' ((Call 'POST' '/api/users' @{} $customerSession.token).StatusCode -eq 403)
$password='QaStudio-'+[Guid]::NewGuid().ToString('N')+'!'
$name='[QA Admin Vendor '+$manifest.ownershipKey+'] Studio'
$create=Call 'POST' '/api/users' @{fullName=$name;password=$password;role='VendorOwner';operatingRegions=@('Hà Nội','Đà Nẵng')} $adminSession.token
Check 'Admin creates vendor without contact details' ($create.StatusCode -eq 200)
$vendor=$create.Content|ConvertFrom-Json
Check 'Generated alias and no invented contacts' ($vendor.loginName -and -not $vendor.email -and -not $vendor.phoneNumber)
Check 'Multiple operating areas round trip' ($vendor.vendorOperatingRegions.Count -eq 2 -and -not $vendor.vendorNationwide)
$vendorSession=Login $vendor.loginName $password
Check 'Generated alias signs into correct vendor' ($vendorSession.user.id -eq $vendor.id -and $vendorSession.user.role -eq 'VendorOwner')
Check 'Display name is not an ambiguous login identifier' ((Call 'POST' '/api/auth/login' @{email=$name;password=$password}).StatusCode -eq 400)
$deniedUpload=Invoke-WebRequest -Uri ($base+'/api/upload/images') -Method Post -Headers @{Authorization='Bearer '+$customerSession.token} -ContentType 'multipart/form-data; boundary=qa-boundary' -Body '--qa-boundary--' -SkipHttpErrorCheck
Check 'Customer cannot upload vendor images' ($deniedUpload.StatusCode -eq 403)
$client=[Net.Http.HttpClient]::new()
$client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$vendorSession.token)
$multipart=[Net.Http.MultipartFormDataContent]::new()
$bytes=[IO.File]::ReadAllBytes((Join-Path $script:qaRepo 'client/public/wedding-match/hero-garden-couple.jpg'))
$image=[Net.Http.ByteArrayContent]::new($bytes)
$image.Headers.ContentType=[Net.Http.Headers.MediaTypeHeaderValue]::new('image/jpeg')
$multipart.Add($image,'files','synthetic-studio.jpg')
$upload=$client.PostAsync($base+'/api/upload/images',$multipart).GetAwaiter().GetResult()
$uploadBody=$upload.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json
Check 'Device image upload persists managed URL' ([int]$upload.StatusCode -eq 200 -and $uploadBody.urls[0] -match '^/uploads/listings/[a-f0-9]{32}\.(jpg|png|webp)$')
$url=$uploadBody.urls[0];$multipart.Dispose();$client.Dispose()
Check 'Uploaded image is readable' ((Call 'GET' $url).StatusCode -eq 200)
$albumId=[Guid]::NewGuid().ToString()
$body=@{categoryId='11111111-1111-1111-1111-111111111103';title='QA Studio persisted media';minPrice=1000000;maxPrice=2000000;description='Synthetic listing for isolated Studio acceptance';location='Hà Nội';imageUrls=@($url);serviceDetails=@{collections=@(@{id=$albumId;name='Bộ sưu tập thử nghiệm';images=@($url)});latestUpdate=@{content='Cập nhật đã lưu vào SQL';imageUrl=$url}}}
$listingResponse=Call 'POST' '/api/listings' $body $vendorSession.token
Check 'Create draft with album and update' ($listingResponse.StatusCode -eq 201)
$listingId=($listingResponse.Content|ConvertFrom-Json).listingId
$editor=(Call 'GET' "/api/listings/$listingId/edit" $null $vendorSession.token).Content|ConvertFrom-Json
Check 'Editor reload retains album and update' ($editor.serviceDetails.collections[0].id -eq $albumId -and $editor.serviceDetails.latestUpdate.content -eq 'Cập nhật đã lưu vào SQL')
Check 'Unpublished listing stays private' ((Call 'GET' "/api/listings/$listingId").StatusCode -eq 404)
& dotnet artifacts/admin-vendor-qa-20261029/build/bin/AdminVendorQa/debug/AdminVendorQa.dll prepare-studio --manifest $manifestPath
if($LASTEXITCODE -ne 0){throw 'Owned synthetic verification setup refused.'}
Check 'Submit listing for moderation' ((Call 'POST' "/api/listings/$listingId/submit" $null $vendorSession.token).StatusCode -eq 200)
Check 'Moderator approves persisted content' ((Call 'POST' "/api/listings/$listingId/approve" $null $modSession.token).StatusCode -eq 200)
$public=(Call 'GET' "/api/listings/$listingId").Content|ConvertFrom-Json
Check 'Approved public details retain album and update' ($public.serviceDetails.collections[0].images[0] -eq $url -and $public.serviceDetails.latestUpdate.imageUrl -eq $url)
$body.serviceDetails.latestUpdate.content='Draft update must stay private'
Check 'Vendor can save new revision' ((Call 'PUT' "/api/listings/$listingId" $body $vendorSession.token).StatusCode -eq 200)
$public=(Call 'GET' "/api/listings/$listingId").Content|ConvertFrom-Json
Check 'Public approved update is unchanged while draft exists' ($public.serviceDetails.latestUpdate.content -eq 'Cập nhật đã lưu vào SQL')
$unsafe=$body.Clone();$unsafe.serviceDetails=@{latestUpdate=@{content='Unsafe image';imageUrl='blob:temporary-not-persisted'}}
Check 'API rejects temporary blob image' ((Call 'PUT' "/api/listings/$listingId" $unsafe $vendorSession.token).StatusCode -eq 400)
$privatePath=Join-Path (Split-Path $manifestPath -Parent) 'browser-studio.json'
if(Test-Path -LiteralPath $privatePath){throw 'Do not overwrite browser actor fixture.'}
@{email=$vendor.loginName;password=$password;listingId=$listingId;imageUrl=$url;albumId=$albumId}|ConvertTo-Json|Set-Content -LiteralPath $privatePath
$checks|ConvertTo-Json -Depth 4|Set-Content (Join-Path $PSScriptRoot 'api-results.json')
@{passed=$checks.Count;failed=0;target='Owned localhost GUID database only'}|ConvertTo-Json


