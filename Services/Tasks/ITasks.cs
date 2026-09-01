using casbin_poc.DTO;

namespace casbin_poc.Services.Tasks
{
    public interface ITasks
    {
        Task<IReadOnlyList<TaskDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<TaskDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<TaskDto> CreateAsync(CreateTaskDto task, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(Guid id, UpdateTaskDto task, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> ShareTaskAsync(Guid taskId, string targetUserId, string action, CancellationToken cancellationToken = default);
        Task<bool> RevokeTaskAsync(Guid taskId, string targetUserId, string action, CancellationToken cancellationToken = default);
    }
}
