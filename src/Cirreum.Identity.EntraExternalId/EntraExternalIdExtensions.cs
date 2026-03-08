namespace Cirreum.Identity.EntraExternalId;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// Extension methods for registering the Entra External ID custom authentication extension endpoint.
/// </summary>
/// <remarks>
/// See SETUP.md for full Azure Portal configuration, appsettings.json reference,
/// and troubleshooting guidance.
/// </remarks>
public static class EntraExternalIdExtensions {

	// Sentinel registered alongside the provisioner so MapEntraExternalId can validate
	// at startup that a provisioner has been registered.
	private sealed class ProvisionerMarker { }

	// -------------------------------------------------------------------------
	// IHostApplicationBuilder overloads (primary — WebApplicationBuilder etc.)
	// -------------------------------------------------------------------------

	/// <summary>
	/// Registers Entra External ID services, configuration, and the
	/// <see cref="IUserProvisioner"/> implementation that controls user access.
	/// </summary>
	/// <typeparam name="TProvisioner">
	/// The provisioner implementation. Must implement <see cref="IUserProvisioner"/>.
	/// Register as scoped to allow access to database contexts and other request-scoped services.
	/// </typeparam>
	/// <param name="builder">The application builder (<c>WebApplicationBuilder</c> etc.).</param>
	/// <param name="sectionName">
	/// The configuration section name. Defaults to
	/// <c>"Cirreum:Identity:EntraExternalId"</c>.
	/// See SETUP.md for the full configuration schema.
	/// </param>
	public static IHostApplicationBuilder AddEntraExternalId<TProvisioner>(
		this IHostApplicationBuilder builder,
		string sectionName = "Cirreum:Identity:EntraExternalId")
		where TProvisioner : class, IUserProvisioner {
		builder.Services.AddEntraExternalId<TProvisioner>(builder.Configuration, sectionName);
		return builder;
	}

	/// <summary>
	/// Registers Entra External ID services, configuration, and the
	/// <see cref="IUserProvisioner"/> implementation using a factory function.
	/// </summary>
	/// <typeparam name="TProvisioner">
	/// The provisioner implementation. Must implement <see cref="IUserProvisioner"/>.
	/// </typeparam>
	/// <param name="builder">The application builder (<c>WebApplicationBuilder</c> etc.).</param>
	/// <param name="factory">Factory function to create the provisioner instance.</param>
	/// <param name="sectionName">
	/// The configuration section name. Defaults to
	/// <c>"Cirreum:Identity:EntraExternalId"</c>.
	/// See SETUP.md for the full configuration schema.
	/// </param>
	public static IHostApplicationBuilder AddEntraExternalId<TProvisioner>(
		this IHostApplicationBuilder builder,
		Func<IServiceProvider, TProvisioner> factory,
		string sectionName = "Cirreum:Identity:EntraExternalId")
		where TProvisioner : class, IUserProvisioner {
		builder.Services.AddEntraExternalId(builder.Configuration, factory, sectionName);
		return builder;
	}

	// -------------------------------------------------------------------------
	// IServiceCollection overloads (for testing and advanced scenarios)
	// -------------------------------------------------------------------------

	/// <summary>
	/// Registers Entra External ID services, configuration, and the
	/// <see cref="IUserProvisioner"/> implementation that controls user access.
	/// </summary>
	/// <typeparam name="TProvisioner">
	/// The provisioner implementation. Must implement <see cref="IUserProvisioner"/>.
	/// Register as scoped to allow access to database contexts and other request-scoped services.
	/// </typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration root.</param>
	/// <param name="sectionName">
	/// The configuration section name. Defaults to
	/// <c>"Cirreum:Identity:EntraExternalId"</c>.
	/// See SETUP.md for the full configuration schema.
	/// </param>
	public static IServiceCollection AddEntraExternalId<TProvisioner>(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionName = "Cirreum:Identity:EntraExternalId")
		where TProvisioner : class, IUserProvisioner {
		services.Configure<EntraExternalIdOptions>(configuration.GetSection(sectionName));
		services.AddSingleton<EntraTokenValidator>();
		services.AddScoped<EntraExternalIdHandler>();
		services.AddScoped<IUserProvisioner, TProvisioner>();
		services.AddSingleton<ProvisionerMarker>();
		return services;
	}

	/// <summary>
	/// Registers Entra External ID services, configuration, and the
	/// <see cref="IUserProvisioner"/> implementation using a factory function.
	/// </summary>
	/// <typeparam name="TProvisioner">
	/// The provisioner implementation. Must implement <see cref="IUserProvisioner"/>.
	/// </typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration root.</param>
	/// <param name="factory">Factory function to create the provisioner instance.</param>
	/// <param name="sectionName">
	/// The configuration section name. Defaults to
	/// <c>"Cirreum:Identity:EntraExternalId"</c>.
	/// See SETUP.md for the full configuration schema.
	/// </param>
	public static IServiceCollection AddEntraExternalId<TProvisioner>(
		this IServiceCollection services,
		IConfiguration configuration,
		Func<IServiceProvider, TProvisioner> factory,
		string sectionName = "Cirreum:Identity:EntraExternalId")
		where TProvisioner : class, IUserProvisioner {
		services.Configure<EntraExternalIdOptions>(configuration.GetSection(sectionName));
		services.AddSingleton<EntraTokenValidator>();
		services.AddScoped<EntraExternalIdHandler>();
		services.AddScoped<IUserProvisioner>(factory);
		services.AddSingleton<ProvisionerMarker>();
		return services;
	}

	// -------------------------------------------------------------------------
	// Endpoint mapping
	// -------------------------------------------------------------------------

	/// <summary>
	/// Maps the anonymous Entra External ID custom authentication extension endpoint.
	/// Route is configurable via <see cref="EntraExternalIdOptions.Route"/>.
	/// </summary>
	/// <remarks>
	/// Register this after <c>UseAuthentication</c> / <c>UseAuthorization</c>.
	/// The endpoint is registered as <c>AllowAnonymous</c> — all authentication is
	/// performed internally by validating the Entra bearer token. See SETUP.md.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// Thrown if <c>AddEntraExternalId&lt;TProvisioner&gt;</c> has not been called.
	/// </exception>
	public static IEndpointRouteBuilder MapEntraExternalId(this IEndpointRouteBuilder app) {
		if (app.ServiceProvider.GetService<ProvisionerMarker>() is null) {
			throw new InvalidOperationException(
				"No IUserProvisioner has been registered. " +
				"Call builder.AddEntraExternalId<TProvisioner>() before calling app.MapEntraExternalId().");
		}

		var options = app.ServiceProvider.GetRequiredService<IOptions<EntraExternalIdOptions>>().Value;
		app.MapPost(options.Route, async (HttpRequest request, EntraExternalIdHandler handler, CancellationToken cancellationToken) =>
			await handler.HandleAsync(request, cancellationToken))
			.AllowAnonymous()
			.ExcludeFromDescription(); // Hide from OpenAPI/Swagger
		return app;
	}

}