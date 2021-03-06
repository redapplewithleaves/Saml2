﻿using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using Kentor.AuthServices.WebSso;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;

namespace Sustainsys.Saml2.AspNetCore2
{
    /// <summary>
    /// Authentication handler for Saml2
    /// </summary>
    public class Saml2Handler : IAuthenticationRequestHandler
    {
        private readonly IOptionsMonitorCache<Saml2Options> optionsCache;
        Saml2Options options;
        HttpContext context;
        private readonly IDataProtector dataProtector;
        AuthenticationScheme scheme;
        private readonly IOptionsFactory<Saml2Options> optionsFactory;

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="optionsCache">Options</param>
        /// <param name="dataProtectorProvider">Data Protector Provider</param>
        /// <param name="optionsFactory">Factory for options</param>
        public Saml2Handler(
            IOptionsMonitorCache<Saml2Options> optionsCache,
            IDataProtectionProvider dataProtectorProvider,
            IOptionsFactory<Saml2Options> optionsFactory)
        {
            if (dataProtectorProvider == null)
            {
                throw new ArgumentNullException(nameof(dataProtectorProvider));
            }

            dataProtector = dataProtectorProvider.CreateProtector(GetType().FullName);

            this.optionsFactory = optionsFactory;
            this.optionsCache = optionsCache;
        }

        /// <InheritDoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1500:VariableNamesShouldNotMatchFieldNames", MessageId = "scheme")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1500:VariableNamesShouldNotMatchFieldNames", MessageId = "context")]
        public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context)
        {
            this.context = context;
            this.scheme = scheme;
            options = optionsCache.GetOrAdd(scheme.Name, () => optionsFactory.Create(scheme.Name));

            return Task.CompletedTask;
        }

        /// <InheritDoc />
        [ExcludeFromCodeCoverage]
        public Task<AuthenticateResult> AuthenticateAsync()
        {
            throw new NotImplementedException();
        }

        /// <InheritDoc />
        public Task ChallengeAsync(AuthenticationProperties properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            // Don't serialize the return url twice, move it to our location.
            var redirectUri = properties.RedirectUri;
            properties.RedirectUri = null;

            var requestData = context.ToHttpRequestData(null);

            var result = SignInCommand.Run(
                null,
                redirectUri,
                requestData,
                options,
                properties.Items);

            result.Apply(context, dataProtector, scheme.Name);

            return Task.CompletedTask;
        }

        /// <InheritDoc />
        [ExcludeFromCodeCoverage]
        public Task ForbidAsync(AuthenticationProperties properties)
        {
            throw new NotImplementedException();
        }

        /// <InheritDoc />
        public Task<bool> HandleRequestAsync()
        {
            if(context.Request.Path.StartsWithSegments(options.SPOptions.ModulePath, StringComparison.Ordinal))
            {
                var commandName = context.Request.Path.Value.Substring(
                    options.SPOptions.ModulePath.Length).TrimStart('/');

                var commandResult = CommandFactory.GetCommand(commandName).Run(
                    context.ToHttpRequestData(dataProtector.Unprotect), options);

                commandResult.Apply(context, dataProtector, options.SignInScheme);

                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}
