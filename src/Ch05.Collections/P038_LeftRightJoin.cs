namespace MasteringModernCSharp.Ch05;

public record Employee(int Id, string Name, int? DepartmentId);
public record Department(int Id, string Name);

public static class StaffDirectory
{
    // Every employee, with their department name or "(none)".
    public static List<(string Employee, string Department)>
        WithDepartments(IEnumerable<Employee> employees,
                        IEnumerable<Department> departments) =>
        employees
            .LeftJoin(departments,
                      e => e.DepartmentId,
                      d => (int?)d.Id,
                      (e, d) => (e.Name, d?.Name ?? "(none)"))
            .ToList();

    // Every department, including those with no employees.
    public static List<(string Department, int Headcount)>
        Headcounts(IEnumerable<Employee> employees,
                   IEnumerable<Department> departments) =>
        employees
            .RightJoin(departments,
                       e => e.DepartmentId,
                       d => (int?)d.Id,
                       (e, d) => (Department: d.Name, Employee: e))
            .GroupBy(x => x.Department)
            .Select(g => (g.Key, g.Count(x => x.Employee is not null)))
            .ToList();
}
