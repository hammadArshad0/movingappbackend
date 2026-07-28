using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace task_full_stack.Controllers.lead
{
    public class LeadController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/Lead?companyId=1&statusId=2&assignedSalesPersonId=5&pageNumber=1&pageSize=10
        // Generic pagination + filtering
        [HttpGet]
        [Route("api/Lead")]
        public IHttpActionResult GetAll(int companyId, int? statusId = null, int? assignedSalesPersonId = null,
                                         int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var query = db.Leads.Where(l => l.CompanyId == companyId);

                if (statusId.HasValue)
                    query = query.Where(l => l.StatusId == statusId.Value);

                if (assignedSalesPersonId.HasValue)
                    query = query.Where(l => l.AssignedSalesPersonId == assignedSalesPersonId.Value);

                var totalCount = query.Count();

                var leads = query
                             .OrderByDescending(l => l.CreatedAt)
                             .Skip((pageNumber - 1) * pageSize)
                             .Take(pageSize)
                             .Select(l => new
                             {
                                 l.Id,
                                 l.CustomerId,
                                 CustomerName = db.Customers.Where(c => c.Id == l.CustomerId).Select(c => c.Name).FirstOrDefault(),
                                 l.AssignedSalesPersonId,
                                 SalesPersonName = db.Users.Where(u => u.Id == l.AssignedSalesPersonId)
                                                    .Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                                 l.StatusId,
                                 StatusName = db.LeadStatuses.Where(s => s.Id == l.StatusId).Select(s => s.Name).FirstOrDefault(),
                                 l.Source,
                                 l.CreatedAt
                             })
                             .ToList();

                return Ok(new
                {
                    totalCount,
                    pageNumber,
                    pageSize,
                    data = leads
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Lead/5
        [HttpGet]
        [Route("api/Lead/{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var lead = db.Leads.FirstOrDefault(l => l.Id == id);

                if (lead == null)
                    return NotFound();

                return Ok(lead);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Lead
        // Capture lead + duplicate detection (same customer, open/active lead already exists)
        [HttpPost]
        [Route("api/Lead")]
        public IHttpActionResult Create([FromBody] LeadDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                if (model.CustomerId <= 0)
                    return BadRequest("CustomerId is required.");

                var customerExists = db.Customers.Any(c => c.Id == model.CustomerId && c.CompanyId == model.CompanyId);
                if (!customerExists)
                    return BadRequest("Invalid CustomerId for this company.");

                // Duplicate detection: same customer ka koi lead already open/pending status mein hai
                // (StatusId list customize kar lena apne actual "Closed/Lost" status ids ke hisab se)
                var duplicateLead = db.Leads.Any(l => l.CustomerId == model.CustomerId
                                                       && l.CompanyId == model.CompanyId
                                                       && l.StatusId == model.StatusId);
                if (duplicateLead)
                    return BadRequest("A similar active lead already exists for this customer.");

                var lead = new Lead
                {
                    CompanyId = model.CompanyId,
                    CustomerId = model.CustomerId,
                    AssignedSalesPersonId = model.AssignedSalesPersonId,
                    StatusId = model.StatusId,
                    Source = model.Source,
                    CreatedAt = DateTime.Now
                };

                db.Leads.Add(lead);
                db.SaveChanges();

                return Ok(new { message = "Lead created successfully.", id = lead.Id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Lead/5
        [HttpPut]
        [Route("api/Lead/{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] LeadDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var lead = db.Leads.FirstOrDefault(l => l.Id == id);

                if (lead == null)
                    return NotFound();

                lead.CustomerId = model.CustomerId;
                lead.AssignedSalesPersonId = model.AssignedSalesPersonId;
                lead.StatusId = model.StatusId;
                lead.Source = model.Source;

                db.SaveChanges();

                return Ok(new { message = "Lead updated successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Lead/5/assign
        // Alag se salesperson assign karne ke liye
        [HttpPut]
        [Route("api/Lead/{id:int}/assign")]
        public IHttpActionResult AssignSalesPerson(int id, [FromBody] AssignSalesPersonDto model)
        {
            try
            {
                var lead = db.Leads.FirstOrDefault(l => l.Id == id);

                if (lead == null)
                    return NotFound();

                var userExists = db.Users.Any(u => u.Id == model.SalesPersonId && u.IsDeleted == false);
                if (!userExists)
                    return BadRequest("Invalid SalesPersonId.");

                lead.AssignedSalesPersonId = model.SalesPersonId;
                db.SaveChanges();

                return Ok(new { message = "Salesperson assigned successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Lead/5/status
        // Status track karne ke liye alag endpoint
        [HttpPut]
        [Route("api/Lead/{id:int}/status")]
        public IHttpActionResult ChangeStatus(int id, [FromBody] ChangeStatusDto model)
        {
            try
            {
                var lead = db.Leads.FirstOrDefault(l => l.Id == id);

                if (lead == null)
                    return NotFound();

                var statusExists = db.LeadStatuses.Any(s => s.Id == model.StatusId);
                if (!statusExists)
                    return BadRequest("Invalid StatusId.");

                lead.StatusId = model.StatusId;
                db.SaveChanges();

                return Ok(new { message = "Lead status updated successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/Lead/5
        [HttpDelete]
        [Route("api/Lead/{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var lead = db.Leads.FirstOrDefault(l => l.Id == id);

                if (lead == null)
                    return NotFound();

                // Agar survey ya quote already generate ho chuki hai to delete allow na karo
                var hasSurvey = db.Surveys.Any(s => s.LeadId == id);
                var hasQuote = db.Quotes.Any(q => q.LeadId == id);

                if (hasSurvey || hasQuote)
                    return BadRequest("Cannot delete lead with existing survey or quote records.");

                db.Leads.Remove(lead);
                db.SaveChanges();

                return Ok(new { message = "Lead deleted successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // NOTE: api/LeadStatus is handled by LeadStatusController.
        // The duplicate [Route("api/LeadStatus")] GetAll() that used to live here
        // was removed because two controllers exposing the SAME route caused
        // Web API's route resolver to throw "Multiple actions were found that
        // match the request" at runtime — that was your "format not matching" error.
    }

    public class LeadDto
    {
        public int CompanyId { get; set; }
        public int CustomerId { get; set; }
        public int? AssignedSalesPersonId { get; set; }
        public int? StatusId { get; set; }
        public string Source { get; set; }
    }

    public class AssignSalesPersonDto
    {
        public int SalesPersonId { get; set; }
    }

    public class ChangeStatusDto
    {
        public int StatusId { get; set; }
    }
}