param([switch]$ApplyFixtures)
$ErrorActionPreference = 'Stop'
# This utility never targets the user's ordinary API/database. All credentials below are fictional QA accounts.
$qaBase = 'http://127.0.0.1:5009'
$qaListener = Get-NetTCPConnection -LocalPort 5009 -State Listen | Select-Object -First 1
$qaProcess = Get-CimInstance Win32_Process -Filter ('ProcessId=' + $qaListener.OwningProcess)
if ($qaProcess.CommandLine -notlike '*WeddingPlanUi_0161333*') { throw 'Refusing non-QA database/API.' }
function Invoke-Qa($Method, $Path, $Body, $Headers) {
    $argsQa = @{ Method=$Method; Uri=($qaBase + $Path); Headers=$Headers; ContentType='application/json' }
    if ($null -ne $Body) { $argsQa.Body = ($Body | ConvertTo-Json -Depth 15) }
    Invoke-RestMethod @argsQa
}
$customer = Invoke-Qa Post '/api/auth/login' @{email='journey-cart-qa-20261016@example.invalid';password='QaOnly-Test-2026!'} @{}
if ($customer.user.email -ne 'journey-cart-qa-20261016@example.invalid') { throw 'Unexpected QA account.' }
$headersQa = @{Authorization=('Bearer ' + $customer.token)}
$metadata = Invoke-Qa Get '/api/customer-orders' $null $headersQa
Write-Output ('QA lifecycle metadata: ' + $metadata.Count + ' records; credentials/tokens are not logged.')
if (!$ApplyFixtures) { Write-Output 'Read only. Add -ApplyFixtures to append fictional API fixtures in the named QA database.'; exit }
$builderQa = Invoke-Qa Get '/api/combos/builder' $null $headersQa
$vendorQa = $builderQa.vendors | Where-Object name -eq '[DEMO] Mây Wedding — Trọn gói' | Select-Object -First 1
if (!$vendorQa) { throw 'The explicitly fictional QA vendor was not found.' }
$savedQa = Invoke-Qa Get '/api/combos/orders/my' $null $headersQa
if ($savedQa | Where-Object note -eq '[QA lifecycle 20261017] draft visual fixture') {
    Write-Output 'QA visual fixture already present. Existing records left unchanged.'; exit
}
$templateQa = $builderQa.templates | Where-Object { $_.ownerVendorId -eq $vendorQa.id -and $_.tier -eq 'Saving' } | Select-Object -First 1
$selectionsQa = @($templateQa.items | Where-Object { !$_.isOptional } | ForEach-Object { @{serviceItemId=$_.serviceItemId;variantIds=@();addOnIds=@();hours=1} })
$bodyQa = @{templateId=$templateQa.id;region='Hồ Chí Minh';weddingDate='2027-01-20';guestCount=201;selections=$selectionsQa;listingIds=@()}
$priceQa = Invoke-Qa Post '/api/combos/price' $bodyQa $headersQa
$draftBodyQa = $bodyQa.Clone(); $draftBodyQa.expectedTotal=$priceQa.total; $draftBodyQa.saveAsDraft=$true; $draftBodyQa.note='[QA lifecycle 20261017] draft visual fixture'
$draftQa = Invoke-Qa Post '/api/combos/orders' $draftBodyQa $headersQa
$archivedQa = Invoke-Qa Post ("/api/customer-orders/combo/$($draftQa.id)/archive") @{} $headersQa
if (!$archivedQa.isArchived) { throw 'Archive failed.' }
$restoredQa = Invoke-Qa Post ("/api/customer-orders/combo/$($draftQa.id)/restore") @{} $headersQa
if ($restoredQa.isArchived) { throw 'Restore failed.' }
# Two services produce a compact consultation group; one quote stays open and one becomes an unpaid pending proposal.
$bodyQa.templateId=$null; $bodyQa.selections=@($selectionsQa | Select-Object -First 2)
$priceQa = Invoke-Qa Post '/api/combos/price' $bodyQa $headersQa
$bodyQa.expectedTotal=$priceQa.total; $bodyQa.phoneNumber='0900000999'; $bodyQa.note='[QA lifecycle 20261017] fictional consultation, no payment'
$orderQa = Invoke-Qa Post '/api/combos/orders' $bodyQa $headersQa
$vendorLoginQa = Invoke-Qa Post '/api/auth/login' @{email='qa-full@weddingmatch.example';password='DemoWeddingOnly!2026'} @{}
if ($vendorLoginQa.user.email -ne 'qa-full@weddingmatch.example') { throw 'Unexpected QA vendor login.' }
$vendorHeadersQa = @{Authorization=('Bearer ' + $vendorLoginQa.token)}
$indexQa=0
foreach ($lineQa in $orderQa.items) {
    $null = Invoke-Qa Post ("/api/leads/$($lineQa.leadId)/accept") @{} $vendorHeadersQa
    $quoteQa = Invoke-Qa Post '/api/quotes' @{leadId=$lineQa.leadId;items=@(@{name=('[QA] ' + $lineQa.title);quantity=1;unitPrice=($lineQa.price-$lineQa.discountShare)});depositAmount=0;terms='Đề xuất QA hư cấu. Không ký kết, không chuyển tiền.';validDays=7;vatRate=0} $vendorHeadersQa
    if ($indexQa -eq 0) {
        $pendingQa=Invoke-Qa Post ("/api/quotes/$($quoteQa.id)/accept") @{} $headersQa
        # Accepting this fixture only creates PendingVerification, never confirm/sign it.
        $contractsQa = Invoke-Qa Get '/api/contracts/my-contracts' $null $headersQa
        $proposalQa = $contractsQa | Where-Object id -eq $pendingQa.contractId
        if ($proposalQa.status -ne 'PendingVerification') { throw 'Fixture must remain an unconfirmed proposal.' }
    }
    $indexQa++
}
Write-Output ("PASS: archive/restore persisted. Appended fictional draft $($draftQa.code), consultation $($orderQa.code), one open quote and one unpaid pending proposal. No contract confirmed, no payments or refunds.")
