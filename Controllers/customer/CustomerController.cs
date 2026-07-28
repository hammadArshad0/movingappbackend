using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace task_full_stack.Controllers.customer
{
    public class CustomerController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/Customer?companyId=1&search=ali&pageNumber=1&pageSize=10
        // Generic pagination + search (Name/Email/Phone)
        [HttpGet]
        [Route("api/Customer")]
        public IHttpActionResult GetAll(int companyId, string search = null, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var query = db.Customers.Where(c => c.CompanyId == companyId);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(c =>
                        c.Name.Contains(search) ||
                        c.Email.Contains(search) ||
                        c.Phone.Contains(search));
                }

                var totalCount = query.Count();

                var customers = query
                                 .OrderByDescending(c => c.Id)
                                 .Skip((pageNumber - 1) * pageSize)
                                 .Take(pageSize)
                                 .Select(c => new
                                 {
                                     c.Id,
                                     c.Name,
                                     c.Email,
                                     c.Phone,
                                     c.Address
                                 })
                                 .ToList();

                return Ok(new
                {
                    totalCount,
                    pageNumber,
                    pageSize,
                    data = customers
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Customer/5
        [HttpGet]
        [Route("api/Customer/{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var customer = db.Customers.FirstOrDefault(c => c.Id == id);

                if (customer == null)
                    return NotFound();

                return Ok(customer);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Customer/top-by-revenue?companyId=1&top=10
        // Top N customers by total revenue (sum of paid invoices via their moves)
        [HttpGet]
        [Route("api/Customer/top-by-revenue")]
        public IHttpActionResult GetTopByRevenue(int companyId, int top = 10)
        {
            try
            {
                var result = (from c in db.Customers
                              where c.CompanyId == companyId
                              select new
                              {
                                  c.Id,
                                  c.Name,
                                  c.Email,
                                  TotalRevenue = (from m in db.Moves
                                                  join i in db.Invoices on m.Id equals i.MoveId
                                                  join p in db.Payments on i.Id equals p.InvoiceId
                                                  where m.CustomerId == c.Id
                                                  select (decimal?)p.Amount).Sum() ?? 0
                              })
                              .OrderByDescending(x => x.TotalRevenue)
                              .Take(top)
                              .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Customer
        [HttpPost]
        [Route("api/Customer")]
        public IHttpActionResult Create([FromBody] CustomerDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                if (string.IsNullOrWhiteSpace(model.Name))
                    return BadRequest("Customer Name is required.");

                var companyExists = db.Companies.Any(c => c.Id == model.CompanyId && c.IsDeleted == false);
                if (!companyExists)
                    return BadRequest("Invalid CompanyId.");

                // Duplicate detection - same email/phone already company mein exist na kare
                if (!string.IsNullOrWhiteSpace(model.Email))
                {
                    var duplicateEmail = db.Customers.Any(c => c.CompanyId == model.CompanyId && c.Email == model.Email);
                    if (duplicateEmail)
                        return BadRequest("A customer with this email already exists.");
                }

                var customer = new Customer
                {
                    CompanyId = model.CompanyId,
                    Name = model.Name,
                    Email = model.Email,
                    Phone = model.Phone,
                    Address = model.Address
                };

                db.Customers.Add(customer);
                db.SaveChanges();

                return Ok(new { message = "Customer created successfully.", id = customer.Id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Customer/5
        [HttpPut]
        [Route("api/Customer/{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] CustomerDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var customer = db.Customers.FirstOrDefault(c => c.Id == id);

                if (customer == null)
                    return NotFound();

                customer.Name = model.Name;
                customer.Email = model.Email;
                customer.Phone = model.Phone;
                customer.Address = model.Address;

                db.SaveChanges();

                return Ok(new { message = "Customer updated successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/Customer/5
        [HttpDelete]
        [Route("api/Customer/{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var customer = db.Customers.FirstOrDefault(c => c.Id == id);

                if (customer == null)
                    return NotFound();

                // Agar customer ke leads/moves hain to delete block karo
                var hasLeads = db.Leads.Any(l => l.CustomerId == id);
                var hasMoves = db.Moves.Any(m => m.CustomerId == id);

                if (hasLeads || hasMoves)
                    return BadRequest("Cannot delete customer with existing leads or moves.");

                db.Customers.Remove(customer);
                db.SaveChanges();

                return Ok(new { message = "Customer deleted successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    public class CustomerDto
    {
        public int CompanyId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
    }
}