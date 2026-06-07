using Plants.Models;

namespace Plants.Services;

public interface IPestService
{
    IReadOnlyList<PestIssue> GetPests();
    IReadOnlyList<PestIssue> GetDiseases();
    PestIssue? GetById(int id);
}
