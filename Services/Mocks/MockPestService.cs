using Plants.Models;

namespace Plants.Services.Mocks;

public class MockPestService : IPestService
{
    private readonly List<PestIssue> _issues =
    [
        new PestIssue
        {
            Id = 101,
            Name = "Тля",
            IsDisease = false,
            Severity = "Средний риск",
            Symptoms = "Колонии мелких насекомых на молодых побегах, липкий налет, скрученные листья.",
            Treatment = "Смыть теплым душем, обработать мыльным раствором или инсектицидом для комнатных растений.",
            Prevention = "Осматривать новые листья и держать новые растения на карантине.",
            AccentColor = "#F4B860"
        },
        new PestIssue
        {
            Id = 102,
            Name = "Паутинный клещ",
            IsDisease = false,
            Severity = "Высокий риск",
            Symptoms = "Тонкая паутинка, светлые точки на листьях, сухие кончики.",
            Treatment = "Изолировать растение, повысить влажность, применить акарицид курсом.",
            Prevention = "Регулярный душ листьев и стабильная влажность воздуха.",
            AccentColor = "#FF9F80"
        },
        new PestIssue
        {
            Id = 103,
            Name = "Щитовка",
            IsDisease = false,
            Severity = "Высокий риск",
            Symptoms = "Плотные коричневые бляшки на стеблях и листьях, липкие следы.",
            Treatment = "Снять вредителей ватной палочкой со спиртом, повторить обработку инсектицидом.",
            Prevention = "Проверять нижнюю сторону листьев и не переувлажнять грунт.",
            AccentColor = "#D98E73"
        },
        new PestIssue
        {
            Id = 201,
            Name = "Мучнистая роса",
            IsDisease = true,
            Severity = "Средний риск",
            Symptoms = "Белый мучнистый налет на листьях, замедленный рост.",
            Treatment = "Удалить пораженные части, улучшить проветривание, применить фунгицид.",
            Prevention = "Не ставить растения слишком плотно и избегать сырости на листьях.",
            AccentColor = "#BFD7EA"
        },
        new PestIssue
        {
            Id = 202,
            Name = "Корневая гниль",
            IsDisease = true,
            Severity = "Критический риск",
            Symptoms = "Вялость при влажном грунте, темные мягкие корни, неприятный запах.",
            Treatment = "Достать растение, удалить поврежденные корни, пересадить в свежий субстрат.",
            Prevention = "Использовать дренаж и поливать только после просушки.",
            AccentColor = "#9B5DE5"
        },
        new PestIssue
        {
            Id = 203,
            Name = "Пятнистость листьев",
            IsDisease = true,
            Severity = "Низкий риск",
            Symptoms = "Желтые или бурые пятна, иногда с сухой каймой.",
            Treatment = "Удалить поврежденные листья, снизить влажность, обработать фунгицидом при распространении.",
            Prevention = "Поливать под корень и не оставлять воду на листьях.",
            AccentColor = "#8ECAE6"
        }
    ];

    public IReadOnlyList<PestIssue> GetPests()
    {
        // TODO: подключить API для получения справочника вредителей.
        return _issues.Where(issue => !issue.IsDisease).OrderBy(issue => issue.Name).ToList();
    }

    public IReadOnlyList<PestIssue> GetDiseases()
    {
        // TODO: подключить API для получения справочника болезней.
        return _issues.Where(issue => issue.IsDisease).OrderBy(issue => issue.Name).ToList();
    }

    public PestIssue? GetById(int id)
    {
        // TODO: подключить API для загрузки карточки проблемы по идентификатору.
        return _issues.FirstOrDefault(issue => issue.Id == id);
    }
}
