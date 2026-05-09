using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Filters;

namespace WebHomestay.Controllers
{
    [AdminAuthorize] // Yêu cầu đăng nhập Admin
    [Route("admin/branches")]
    public class AdminBranchesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminBranchesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: admin/branches
        [AdminAuthorize(Permission = "branches.view")]
        public async Task<IActionResult> Index()
        {
            return View(await _context.Branches.ToListAsync());
        }

        // GET: admin/branches/details/5
        [AdminAuthorize(Permission = "branches.view")]
        [HttpGet("details/{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var branch = await _context.Branches
                .Include(b => b.Rooms)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (branch == null) return NotFound();

            return View(branch);
        }

        // GET: admin/branches/create
        [AdminAuthorize(Permission = "branches.create")]
        [HttpGet("create")]
        public IActionResult Create()
        {
            return View();
        }

        [AdminAuthorize(Permission = "branches.create")]
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Address,Hotline,Description")] Branch branch)
        {
            if (ModelState.IsValid)
            {
                _context.Add(branch);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(branch);
        }

        // GET: admin/branches/edit/5
        [AdminAuthorize(Permission = "branches.edit")]
        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();
            return View(branch);
        }

        [AdminAuthorize(Permission = "branches.edit")]
        [HttpPost("edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,Hotline,Description")] Branch branch)
        {
            if (id != branch.Id) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(branch);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BranchExists(branch.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(branch);
        }

        private bool BranchExists(int id)
        {
            return _context.Branches.Any(e => e.Id == id);
        }
    }
}
