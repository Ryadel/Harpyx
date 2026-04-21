global using Microsoft.AspNetCore.Authentication;
global using Microsoft.AspNetCore.Authentication.Cookies;
global using Microsoft.AspNetCore.Authentication.Google;
global using Microsoft.AspNetCore.Authentication.OpenIdConnect;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Identity.Web;
global using Microsoft.IdentityModel.Protocols.OpenIdConnect;
global using Microsoft.AspNetCore.Authentication.OAuth;

global using System.Threading;
global using System.Threading.Tasks;

global using Serilog;

global using Harpyx.Application.Extensions;
global using Harpyx.Application.Interfaces;
global using Harpyx.Application.Services;
global using Harpyx.Application.Filters;
global using Harpyx.Domain.Entities;
global using Harpyx.Domain.Enums;
global using Harpyx.Infrastructure.Configuration;
global using Harpyx.Infrastructure.Data;
global using Harpyx.Infrastructure.Extensions;
global using Harpyx.WebApp.Extensions;

global using Harpyx.WebApp.Security;
