using casbin_poc.Attributes;
using casbin_poc.DTO;
using casbin_poc.Services.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace casbin_poc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TasksController : ControllerBase
    {
        private readonly ITasks _tasksService;

        public TasksController(ITasks tasksService)
        {
            _tasksService = tasksService;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetAll()
        {
            var tasks = await _tasksService.GetAllAsync(HttpContext.RequestAborted);
            return Ok(tasks);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<TaskDto>> GetById(Guid id)
        {
            var task = await _tasksService.GetByIdAsync(id, HttpContext.RequestAborted);

            return task is null ? NotFound() : Ok(task);
        }

        [HttpPost]
        public async Task<ActionResult<TaskDto>> Create([FromBody] CreateTaskDto task)
        {
            var createdTask = await _tasksService.CreateAsync(task, HttpContext.RequestAborted);

            return CreatedAtAction(nameof(GetById), new { id = createdTask.Id }, createdTask);
        }

        [HttpPost("share")]
        public async Task<ActionResult<bool>> ShareTask([FromBody] ShareTaskDto share)
        {
            var shareTask = await _tasksService.ShareTaskAsync(share.TaskId, share.UserEmail, share.Action.ToString(), HttpContext.RequestAborted);

            return Ok(shareTask);
        }

        [HttpDelete("revoke")]
        public async Task<ActionResult<bool>> RevokeShareTask([FromBody] ShareTaskDto share)
        {
            var revokeTask = await _tasksService.RevokeTaskAsync(share.TaskId, share.UserEmail, share.Action.ToString(), HttpContext.RequestAborted);

            return Ok(revokeTask);
        }

        [HttpPut("{id:guid}")]
        [CasbinAuthorize(resourceType: "task", action: "edit", idRouteParam: "id")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskDto task)
        {
            var updated = await _tasksService.UpdateAsync(id, task, HttpContext.RequestAborted);

            return updated ? NoContent() : NotFound();
        }

        [HttpDelete("{id:guid}")]
        [CasbinAuthorize(resourceType: "task", action: "delete", idRouteParam: "id")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _tasksService.DeleteAsync(id, HttpContext.RequestAborted);

            return deleted ? NoContent() : NotFound();
        }
    }
}
