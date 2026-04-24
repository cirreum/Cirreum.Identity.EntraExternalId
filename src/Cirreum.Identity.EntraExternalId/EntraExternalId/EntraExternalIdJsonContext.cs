namespace Cirreum.Identity.EntraExternalId;

using System.Text.Json.Serialization;
using Cirreum.Identity.EntraExternalId.Models;

[JsonSerializable(typeof(EntraClaimsRequest))]
[JsonSerializable(typeof(EntraClaimsResponse))]
internal sealed partial class EntraExternalIdJsonContext : JsonSerializerContext {
}
