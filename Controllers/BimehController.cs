using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Data;
using OfficeAutomation.Models;

namespace OfficeAutomation.Controllers
{
    public class BimehController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BimehController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var lists = _context.InsuranceLists.ToList();
            return View(lists);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(InsuranceCreateVM model)
        {
            var list = new InsuranceList
            {
                ProjectName = model.ProjectName,
                ManagerName = model.ManagerName,
                Month = model.Month,
                Year = model.Year,
                Status = "Draft",
                CreatedDate = DateTime.Now
            };

            _context.InsuranceLists.Add(list);
            _context.SaveChanges();

            foreach (var emp in model.Employees)
            {
                emp.InsuranceListId = list.Id;
                _context.InsuranceEmployees.Add(emp);
            }

            _context.SaveChanges();

            return RedirectToAction("Index");
        }
    }


}
