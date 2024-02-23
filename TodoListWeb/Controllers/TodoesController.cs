using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoListWeb.Data;
using TodoListWeb.Model;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace TodoListWeb.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TodoesController : ControllerBase
    {
        private readonly TodoListWebContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TodoesController(TodoListWebContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // retrieve ID of the current user
        private int GetCurrentUserId()
        {
            // Get the user's identity from the HttpContext
            var userIdClaim = _httpContextAccessor.HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                Console.WriteLine($"User ID claim value: {userId}");
                return userId;
            }
            else
            {
                Console.WriteLine("User ID claim not found or invalid");
                return -1;
            }
        }

        // GET: api/Todoes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Todo>>> GetTodo()
        {
            return await _context.Todo.ToListAsync(); //pwede na kunin authenticated user, gawa where clause
        }

        // GET: api/Todoes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Todo>> GetTodo(int id)
        {
            var todo = await _context.Todo.FindAsync(id);

            if (todo == null)
            {
                return NotFound();
            }

            return todo;
        }

        // PUT: api/Todoes/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTodo(int id, Todo todo)
        {
            var userId = GetCurrentUserId();

            // Retrieve the task from the database
            var existingTodo = await _context.Todo.FindAsync(id);

            if (existingTodo == null)
            {
                return NotFound();
            }

            // Check if the user is authorized for update
            if (existingTodo.UserId != userId)
            {
                return Forbid();
            }

            // Update the task
            existingTodo.Title = todo.Title;
            existingTodo.Description = todo.Description;
            existingTodo.Status = todo.Status;

            /*if (id != todo.id)
            {
                return BadRequest();
            }

            _context.Entry(todo).State = EntityState.Modified;
*/
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TodoExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Todoes
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Todo>> PostTodo(Todo todo)
        {
            var userId = GetCurrentUserId();

            if (userId == -1)
            {
                return Unauthorized();
            }

            // Associate the task with the current user
            todo.UserId = userId;

            _context.Todo.Add(todo);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTodo", new { id = todo.Id }, todo);

        }

        // DELETE: api/Todoes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTodo(int id)
        {
            var userId = GetCurrentUserId();

            var todo = await _context.Todo.FindAsync(id);
            if (todo == null)
            {
                return NotFound();
            }

            // Check user kung authorized for delete
            if (todo.UserId != userId)
            {
                return Forbid(); 
            }

            _context.Todo.Remove(todo);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TodoExists(int id)
        {
            return _context.Todo.Any(e => e.Id == id);
        }
    }
}
