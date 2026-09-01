using System.Security.Claims;
using Casbin;
using casbin_poc.Data;
using casbin_poc.DTO;
using Microsoft.EntityFrameworkCore;

namespace casbin_poc.Services.Tasks
{
    public class TasksService : ITasks
    {
        private readonly AppDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IEnforcer _enforcer;

        public TasksService(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor, IEnforcer enforcer)
        {
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;
            _enforcer = enforcer;
        }

        public async Task<IReadOnlyList<TaskDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var currentUserId = await GetOwnerIdAsync(cancellationToken);
            var sub = currentUserId.ToString();

            var permissions = _enforcer.GetPermissionsForUser(sub);

            var permissionMap = permissions
                .Select(p => p.ToList())
                .Where(p => p.Count >= 3 && p[1].StartsWith("task:"))
                .Select(p => new
                {
                    TaskIdStr = p[1]["task:".Length..],
                    Action = p[2]
                })
                .Where(x => Guid.TryParse(x.TaskIdStr, out _))
                .GroupBy(x => Guid.Parse(x.TaskIdStr))
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.Action).FirstOrDefault() ?? "read" // Pega a permissão principal
                );

            var taskIds = permissionMap.Keys.ToList();

            var tasks = await _dbContext.Tasks
                .AsNoTracking()
                .Where(task => taskIds.Contains(task.Id))
                .OrderByDescending(task => task.Timestamp)
                .ToListAsync(cancellationToken);

            return tasks.Select(task => new TaskDto
            {
                Id = task.Id,
                Title = task.Title,
                Status = task.Status,
                DueDate = task.DueDate,
                Timestamp = task.Timestamp,
                // 🔐 Injeta a permissão que o usuário tem nesta tarefa
                Permission = permissionMap.TryGetValue(task.Id, out var perm) ? perm : "read"
            }).ToList();
        }

        public async Task<TaskDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var ownerId = await GetOwnerIdAsync(cancellationToken);

            return await _dbContext.Tasks
                .AsNoTracking()
                .Where(task => task.Id == id && task.OwnerId == ownerId)
                .Select(task => new TaskDto
                {
                    Id = task.Id,
                    Title = task.Title,
                    Status = task.Status,
                    DueDate = task.DueDate,
                    Timestamp = task.Timestamp
                })
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<TaskDto> CreateAsync(CreateTaskDto task, CancellationToken cancellationToken = default)
        {
            var ownerId = await GetOwnerIdAsync(cancellationToken);
            var entity = ToModel(task, ownerId);

            _dbContext.Tasks.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var sub = ownerId.ToString();
            var obj = $"task:{entity.Id}";
            await _enforcer.AddNamedPolicyAsync("p", sub, obj, "owner");

            return ToDto(entity);
        }

        public async Task<bool> ShareTaskAsync(Guid taskId, string targetUserEmail, string action, CancellationToken cancellationToken = default)
        {
            var sub = await GetIdFromEmailAsync(targetUserEmail, cancellationToken);
            var obj = $"task:{taskId}";
            var act = action;

            await _enforcer.AddNamedPolicyAsync("p", sub.ToString(), obj, act);
            return true;
        }

        public async Task<bool> RevokeTaskAsync(Guid taskId, string targetUserEmail, string action, CancellationToken cancellationToken = default)
        {
            var sub = await GetIdFromEmailAsync(targetUserEmail, cancellationToken);
            var obj = $"task:{taskId}";
            var act = action;

            await _enforcer.RemoveNamedPolicyAsync("p", sub.ToString(), obj, act);
            return true;
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateTaskDto task, CancellationToken cancellationToken = default)
        {
            var ownerId = await GetOwnerIdAsync(cancellationToken);
            var entity = await _dbContext.Tasks
                .SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            entity.Title = task.Title;
            entity.Status = task.Status;
            entity.DueDate = task.DueDate;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var ownerId = await GetOwnerIdAsync(cancellationToken);
            var entity = await _dbContext.Tasks
                .SingleOrDefaultAsync(task => task.Id == id && task.OwnerId == ownerId, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            _dbContext.Tasks.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _enforcer.RemoveFilteredNamedPolicyAsync("p", 1, $"task:{id}");

            return true;
        }

        private async Task<Guid> GetOwnerIdAsync(CancellationToken cancellationToken)
        {
            var auth0Sub = _httpContextAccessor.HttpContext?.User.FindFirstValue("sub")
                ?? throw new UnauthorizedAccessException("Token sem o claim obrigatório 'sub'.");

            var ownerId = await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.Auth0Sub == auth0Sub)
                .Select(user => (Guid?)user.Id)
                .SingleOrDefaultAsync(cancellationToken);

            return ownerId
                ?? throw new UnauthorizedAccessException("Usuário autenticado não encontrado.");
        }

        private async Task<Guid> GetIdFromEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var ownerId = await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.Email == email)
                .Select(user => (Guid?)user.Id)
                .SingleOrDefaultAsync(cancellationToken);
            return ownerId
                ?? throw new Exception($"Usuário com email '{email}' não encontrado.");
        }

        private static Models.Tasks ToModel(CreateTaskDto task, Guid ownerId)
        {
            return new Models.Tasks
            {
                Id = Guid.NewGuid(),
                Title = task.Title,
                DueDate = task.DueDate,
                OwnerId = ownerId
            };
        }

        private static TaskDto ToDto(Models.Tasks task)
        {
            return new TaskDto
            {
                Id = task.Id,
                Title = task.Title,
                Status = task.Status,
                DueDate = task.DueDate,
                Timestamp = task.Timestamp
            };
        }
    }
}
