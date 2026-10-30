using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings;

/// <summary>Descriptive, moderated listing content persisted in the existing ServiceDetailsJson snapshot.</summary>
internal static class ListingStudioContent
{
    internal static List<ServiceCollectionDto> Collections(List<ServiceCollectionDto>? collections)
    {
        if (collections == null) return [];
        if (collections.Count > 10) throw new DomainException("Tối đa 10 bộ sưu tập cho mỗi dịch vụ.");
        var result = new List<ServiceCollectionDto>(collections.Count);
        var ids = new HashSet<Guid>();
        foreach (var collection in collections)
        {
            if (collection == null || !Guid.TryParse(collection.Id, out var id) || id == Guid.Empty || !ids.Add(id))
                throw new DomainException("Mã bộ sưu tập cần là GUID hợp lệ và không trùng nhau.");
            var name = (collection.Name ?? string.Empty).Trim();
            if (name.Length is < 1 or > 100)
                throw new DomainException("Tên bộ sưu tập cần từ 1 đến 100 ký tự.");
            if (collection.Images is not { Count: >= 1 and <= 10 })
                throw new DomainException("Mỗi bộ sưu tập cần từ 1 đến 10 ảnh.");
            var images = collection.Images.Select(Image).ToList();
            if (images.Distinct(StringComparer.Ordinal).Count() != images.Count)
                throw new DomainException("Ảnh trong cùng một bộ sưu tập không được trùng nhau.");
            result.Add(new ServiceCollectionDto(id.ToString("D"), name, images));
        }
        return result;
    }

    internal static ServiceLatestUpdateDto? LatestUpdate(ServiceLatestUpdateDto? update)
    {
        if (update == null) return null;
        var content = (update.Content ?? string.Empty).Trim();
        if (content.Length > 4000) throw new DomainException("Nội dung cập nhật tối đa 4.000 ký tự.");
        var image = string.IsNullOrWhiteSpace(update.ImageUrl) ? string.Empty : Image(update.ImageUrl);
        return content.Length == 0 && image.Length == 0 ? null : new ServiceLatestUpdateDto(content, image);
    }

    private static string Image(string? image)
    {
        var value = (image ?? string.Empty).Trim();
        if (!ListingImageReferences.IsValid(value) || value.Any(char.IsWhiteSpace) ||
            (!value.StartsWith("/uploads/listings/", StringComparison.Ordinal) &&
             (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)))
            throw new DomainException("Ảnh bộ sưu tập/cập nhật cần ảnh dịch vụ đã tải lên hoặc HTTPS hợp lệ, tối đa 500 ký tự.");
        return value;
    }
}
