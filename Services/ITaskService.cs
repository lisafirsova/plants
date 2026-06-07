using Plants.Models;

namespace Plants.Services;

public interface ITaskService
{
    IReadOnlyList<CareTask> GetTasks();
    CareTask UpdateTaskStatus(int taskId, CareTaskStatus status);
}
