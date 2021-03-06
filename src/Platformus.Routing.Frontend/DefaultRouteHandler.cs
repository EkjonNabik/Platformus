﻿// Copyright © 2017 Dmitry Sikorsky. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Platformus.Barebone;
using Platformus.Globalization.Frontend;
using Platformus.Routing.Data.Abstractions;
using Platformus.Routing.Data.Entities;
using Platformus.Routing.EndpointResolvers;
using Platformus.Routing.Endpoints;
using Platformus.Security;
using Platformus.Security.Data.Abstractions;
using Platformus.Security.Data.Entities;

namespace Platformus.Routing.Frontend
{
  public class DefaultRouteHandler : IDefaultRouteHandler
  {
    public IActionResult TryHandle(IRequestHandler requestHandler, string url)
    {
      IEndpointResolver endpointResolver = requestHandler.GetService<IEndpointResolver>();
      Endpoint endpoint = endpointResolver.GetEndpoint(requestHandler, url);

      if (endpoint == null)
        return null;

      if (endpoint.DisallowAnonymous)
      {
        if (!requestHandler.HttpContext.User.Identity.IsAuthenticated || !this.HasRequiredClaims(requestHandler, endpoint))
        {
          if (string.IsNullOrEmpty(endpoint.SignInUrl))
            throw new HttpException(403, "Access denied.");

          return (requestHandler as Controller).Redirect(endpoint.SignInUrl);
        }
      }

      IEndpoint endpointInstance = this.GetEndpointInstance(endpoint);

      if (endpointInstance == null)
        return null;

      return endpointInstance.Invoke(requestHandler, endpoint, endpointResolver.GetArguments(endpoint.UrlTemplate, url));
    }

    private IEndpoint GetEndpointInstance(Endpoint endpoint)
    {
      return StringActivator.CreateInstance<IEndpoint>(endpoint.CSharpClassName);
    }

    private bool HasRequiredClaims(IRequestHandler requestHandler, Endpoint endpoint)
    {
      IEnumerable<EndpointPermission> endpointPermissions = requestHandler.Storage.GetRepository<IEndpointPermissionRepository>().FilteredByEndpointId(endpoint.Id);

      if (endpointPermissions.Count() == 0 || requestHandler.HttpContext.User.HasClaim(PlatformusClaimTypes.Permission, Platformus.Security.Permissions.DoEverything))
        return true;

      foreach (EndpointPermission endpointPermission in endpointPermissions)
      {
        Permission permission = requestHandler.Storage.GetRepository<IPermissionRepository>().WithKey(endpointPermission.PermissionId);

        if (!requestHandler.HttpContext.User.HasClaim(PlatformusClaimTypes.Permission, permission.Code))
          return false;
      }

      return true;
    }
  }
}