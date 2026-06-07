using Plants.Models;

namespace Plants.Services.Mocks;

public class MockTaskService : ITaskService
{
    private readonly List<CareTask> _tasks =
    [
        new CareTask
        {
            Id = 1,
            PlantId = 1,
            PlantName = "Монстера",
            Title = "Проверить влажность грунта",
            Description = "Палец или индикатор влажности на глубину 3 см.",
            DueDate = DateTime.Today,
            Status = CareTaskStatus.Planned
        },
        new CareTask
        {
            Id = 2,
            PlantId = 2,
            PlantName = "Фикус",
            Title = "Осмотреть листья",
            Description = "Проверить нижнюю сторону листьев на щитовку.",
            DueDate = DateTime.Today.AddDays(1),
            Status = CareTaskStatus.Planned
        },
        new CareTask
        {
            Id = 3,
            PlantId = 3,
            PlantName = "Кактус",
            Title = "Не поливать",
            Description = "Грунт еще сухой период проходит нормально.",
            DueDate = DateTime.Today.AddDays(2),
            Status = CareTaskStatus.Done
        },
        new CareTask
        {
            Id = 4,
            PlantId = 4,
            PlantName = "Спатифиллум",
            Title = "Опрыскать воздух рядом",
            Description = "Не мочить цветы, увлажнить пространство вокруг.",
            DueDate = DateTime.Today,
            Status = CareTaskStatus.Planned
        },
        new CareTask
        {
            Id = 5,
            PlantId = 5,
            PlantName = "Сансевиерия",
            Title = "Повернуть горшок",
            Description = "Развернуть к свету другой стороной.",
            DueDate = DateTime.Today.AddDays(3),
            Status = CareTaskStatus.Skipped
        }
    ];

    public IReadOnlyList<CareTask> GetTasks()
    {
        // TODO: подключить API для получения задач ухода и статусов.
        return _tasks.OrderBy(task => task.DueDate).ToList();
    }

    public CareTask UpdateTaskStatus(int taskId, CareTaskStatus status)
    {
        // TODO: подключить API для синхронизации статуса задачи.
        var task = _tasks.FirstOrDefault(item => item.Id == taskId)
            ?? throw new InvalidOperationException("Задача не найдена.");

        task.Status = status;
        return task;
    }
}
