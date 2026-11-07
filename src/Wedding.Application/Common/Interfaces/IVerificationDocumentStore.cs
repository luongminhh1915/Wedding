namespace Wedding.Application.Common.Interfaces;

/// <summary>Checks existing private managed evidence without treating a URL as proof of ownership.</summary>
public interface IVerificationDocumentStore
{
    bool IsOwnedExistingReference(Guid vendorId, string reference);
}
