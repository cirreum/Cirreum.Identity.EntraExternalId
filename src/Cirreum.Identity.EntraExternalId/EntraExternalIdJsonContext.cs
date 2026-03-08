namespace Cirreum.Identity.EntraExternalId;

using Cirreum.Identity.EntraExternalId.Models;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(EntraClaimsResponse))]
[JsonSerializable(typeof(EntraClaimsRequest))]
internal sealed partial class EntraExternalIdJsonContext : JsonSerializerContext {
}