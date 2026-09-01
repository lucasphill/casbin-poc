using Casbin;
using casbin_poc.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace casbin_poc.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class CasbinAuthorizeAttribute : TypeFilterAttribute
    {
        public CasbinAuthorizeAttribute(string resourceType, string action, string? idRouteParam = null)
            : base(typeof(CasbinAuthorizeFilter))
        {
            Arguments = new object?[] { resourceType, action, idRouteParam };
        }
    }

    public class CasbinAuthorizeFilter : IAsyncActionFilter
    {
        private readonly IEnforcer _enforcer;
        private readonly AppDbContext _dbContext;
        private readonly string _resourceType;
        private readonly string _action;
        private readonly string? _idRouteParam;

        public CasbinAuthorizeFilter(
            IEnforcer enforcer,
            AppDbContext dbContext,
            string resourceType,
            string action,
            string? idRouteParam = null)
        {
            _enforcer = enforcer;
            _dbContext = dbContext;
            _resourceType = resourceType;
            _action = action;
            _idRouteParam = idRouteParam;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // 1. Obtém o Subject (sub) do usuário logado
            var auth0Sub = context.HttpContext.User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(auth0Sub))
            {
                context.Result = new UnauthorizedResult(); // 401
                return;
            }

            // Busca o Id do usuário no banco (caso suas regras Casbin usem Guid em vez de auth0Sub)
            var userId = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Auth0Sub == auth0Sub)
                .Select(u => (Guid?)u.Id)
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

            if (userId is null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var sub = userId.Value.ToString();

            // 2. Monta o Objeto (obj): "task:123" ou apenas "tasks"
            string obj;
            if (!string.IsNullOrEmpty(_idRouteParam))
            {
                if (!context.RouteData.Values.TryGetValue(_idRouteParam, out var routeValue) || routeValue is null)
                {
                    context.Result = new BadRequestObjectResult($"Parâmetro de rota '{_idRouteParam}' não encontrado.");
                    return;
                }

                obj = $"{_resourceType}:{routeValue}";
            }
            else
            {
                obj = _resourceType; // Ex: "tasks"
            }

            // 3. Executa a validação no Casbin
            var isAllowed = await _enforcer.EnforceAsync(sub, obj, _action);

            if (!isAllowed)
            {
                // Retorna 403 Forbidden com mensagem clara
                context.Result = new ObjectResult(new { message = $"Acesso negado para executar '{_action}' no recurso '{obj}'." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            // Se autorizado, prossegue para o método do Controller
            await next();
        }
    }
}
