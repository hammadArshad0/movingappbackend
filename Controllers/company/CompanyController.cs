using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace task_full_stack.Controllers.company
{
    public class CompanyController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/Company
        // Sab active companies (soft-deleted exclude)
        [HttpGet]
        [Route("api/Company")]
        public IHttpActionResult GetAll()
        {
            try
            {
                var companies = db.Companies
                                   .Where(c => c.IsDeleted == false)
                                   .OrderByDescending(c => c.CreatedAt)
                                   .Select(c => new
                                   {
                                       c.Id,
                                       c.Name,
                                       c.Currency,
                                       c.Theme,
                                       c.UnitSystem,
                                       c.CreatedAt
                                   })
                                   .ToList();

                return Ok(companies);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Company/5
        [HttpGet]
        [Route("api/Company/{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var company = db.Companies
                                 .Where(c => c.Id == id && c.IsDeleted == false)
                                 .FirstOrDefault();

                if (company == null)
                    return NotFound();

                return Ok(company);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Company
        [HttpPost]
        [Route("api/Company")]
        public IHttpActionResult Create([FromBody] CompanyDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                if (string.IsNullOrWhiteSpace(model.Name))
                    return BadRequest("Company Name is required.");

                var company = new Company
                {
                    Name = model.Name,
                    Currency = model.Currency,
                    Theme = model.Theme,
                    UnitSystem = model.UnitSystem,
                    CreatedAt = DateTime.Now,
                    IsDeleted = false
                };

                db.Companies.Add(company);
                db.SaveChanges();

                return Ok(new { message = "Company created successfully.", id = company.Id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Company/5
        [HttpPut]
        [Route("api/Company/{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] CompanyDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var company = db.Companies
                                 .Where(c => c.Id == id && c.IsDeleted == false)
                                 .FirstOrDefault();

                if (company == null)
                    return NotFound();

                company.Name = model.Name;
                company.Currency = model.Currency;
                company.Theme = model.Theme;
                company.UnitSystem = model.UnitSystem;

                db.SaveChanges();

                return Ok(new { message = "Company updated successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/Company/5
        // Soft delete
        [HttpDelete]
        [Route("api/Company/{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var company = db.Companies
                                 .Where(c => c.Id == id && c.IsDeleted == false)
                                 .FirstOrDefault();

                if (company == null)
                    return NotFound();

                company.IsDeleted = true;
                db.SaveChanges();

                return Ok(new { message = "Company deleted successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    // Simple DTO to keep request payload clean
    public class CompanyDto
    {
        public string Name { get; set; }
        public string Currency { get; set; }
        public string Theme { get; set; }
        public string UnitSystem { get; set; }
    }
}