using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Script.Serialization;

namespace task_full_stack.Controllers.quote
{
    public class QuoteController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // Configurable threshold - is amount se zyada ki quote sirf Manager approve kar sakta hai
        private readonly decimal _managerApprovalThreshold = 500000;

        // GET api/Quote?leadId=1
        [HttpGet]
        [Route("api/Quote")]
        public IHttpActionResult GetAll(int? leadId = null)
        {
            try
            {
                var query = db.Quotes.AsQueryable();

                if (leadId.HasValue)
                    query = query.Where(q => q.LeadId == leadId.Value);

                var quotes = query
                              .OrderByDescending(q => q.Id)
                              .Select(q => new
                              {
                                  q.Id,
                                  q.LeadId,
                                  q.VersionNo,
                                  q.SubTotal,
                                  q.Discount,
                                  q.Tax,
                                  q.TotalAmount,
                                  q.Status,
                                  q.ApprovedBy,
                                  q.ApprovedDate
                              })
                              .ToList();

                return Ok(quotes);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Quote/5
        [HttpGet]
        [Route("api/Quote/{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var quote = db.Quotes.FirstOrDefault(q => q.Id == id);

                if (quote == null)
                    return NotFound();

                return Ok(quote);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Quote/5/history
        // Version history dekhne ke liye
        [HttpGet]
        [Route("api/Quote/{id:int}/history")]
        public IHttpActionResult GetHistory(int id)
        {
            try
            {
                var history = db.QuoteHistories
                                 .Where(h => h.QuoteId == id)
                                 .OrderByDescending(h => h.VersionNo)
                                 .Select(h => new
                                 {
                                     h.Id,
                                     h.VersionNo,
                                     h.QuoteData
                                 })
                                 .ToList();

                return Ok(history);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Quote
        // Naya quote generate karna (Sub total, discount, tax se total calculate hota hai)
        [HttpPost]
        [Route("api/Quote")]
        public IHttpActionResult Create([FromBody] QuoteDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var leadExists = db.Leads.Any(l => l.Id == model.LeadId);
                if (!leadExists)
                    return BadRequest("Invalid LeadId.");

                var totalAmount = (model.SubTotal - model.Discount) + model.Tax;

                var quote = new Quote
                {
                    LeadId = model.LeadId,
                    VersionNo = 1,
                    SubTotal = model.SubTotal,
                    Discount = model.Discount,
                    Tax = model.Tax,
                    TotalAmount = totalAmount,
                    Status = "Pending"
                };

                db.Quotes.Add(quote);
                db.SaveChanges();

                SaveQuoteSnapshot(quote);

                return Ok(new { message = "Quote generated successfully.", id = quote.Id, totalAmount });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Quote/5
        // Quote update karna -> naya version bante hai, purana history mein save hota hai
        [HttpPut]
        [Route("api/Quote/{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] QuoteDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var quote = db.Quotes.FirstOrDefault(q => q.Id == id);

                if (quote == null)
                    return NotFound();

                if (quote.Status == "Approved")
                    return BadRequest("Cannot modify an already approved quote. Create a new version instead.");

                quote.SubTotal = model.SubTotal;
                quote.Discount = model.Discount;
                quote.Tax = model.Tax;
                quote.TotalAmount = (model.SubTotal - model.Discount) + model.Tax;
                quote.VersionNo = quote.VersionNo + 1;
                quote.Status = "Pending";

                db.SaveChanges();

                SaveQuoteSnapshot(quote);

                return Ok(new { message = "Quote updated successfully.", newVersion = quote.VersionNo });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Quote/5/approve
        // Approval workflow: sirf Manager approve kar sakta hai agar amount threshold se zyada hai
        [HttpPut]
        [Route("api/Quote/{id:int}/approve")]
        public IHttpActionResult Approve(int id, [FromBody] ApproveQuoteDto model)
        {
            try
            {
                var quote = db.Quotes.FirstOrDefault(q => q.Id == id);

                if (quote == null)
                    return NotFound();

                if (quote.Status == "Approved")
                    return BadRequest("Quote is already approved.");

                var approver = db.Users.FirstOrDefault(u => u.Id == model.ApprovedByUserId && u.IsDeleted == false);
                if (approver == null)
                    return BadRequest("Invalid approver.");

                var approverRole = db.Roles.Where(r => r.Id == approver.RoleId).Select(r => r.Name).FirstOrDefault();

                // Business rule: threshold se zyada amount sirf Manager approve kar sakta hai
                if (quote.TotalAmount > _managerApprovalThreshold &&
                    !string.Equals(approverRole, "Manager", StringComparison.OrdinalIgnoreCase))
                {
                    return Content(HttpStatusCode.Forbidden,
                        new { message = $"Only a Manager can approve quotes above {_managerApprovalThreshold}." });
                }

                quote.Status = "Approved";
                quote.ApprovedBy = approver.Id;
                quote.ApprovedDate = DateTime.Now;

                db.SaveChanges();

                return Ok(new { message = "Quote approved successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Quote/5/reject
        [HttpPut]
        [Route("api/Quote/{id:int}/reject")]
        public IHttpActionResult Reject(int id, [FromBody] RejectQuoteDto model)
        {
            try
            {
                var quote = db.Quotes.FirstOrDefault(q => q.Id == id);

                if (quote == null)
                    return NotFound();

                if (quote.Status == "Approved")
                    return BadRequest("Cannot reject an already approved quote.");

                quote.Status = "Rejected";
                db.SaveChanges();

                return Ok(new { message = "Quote rejected.", reason = model?.Reason });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/Quote/5
        [HttpDelete]
        [Route("api/Quote/{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var quote = db.Quotes.FirstOrDefault(q => q.Id == id);

                if (quote == null)
                    return NotFound();

                if (quote.Status == "Approved")
                    return BadRequest("Cannot delete an approved quote.");

                var hasMove = db.Moves.Any(m => m.QuoteId == id);
                if (hasMove)
                    return BadRequest("Cannot delete quote linked to an existing move.");

                var history = db.QuoteHistories.Where(h => h.QuoteId == id).ToList();
                db.QuoteHistories.RemoveRange(history);

                db.Quotes.Remove(quote);
                db.SaveChanges();

                return Ok(new { message = "Quote deleted successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // ================= HELPERS =================

        private void SaveQuoteSnapshot(Quote quote)
        {
            var serializer = new JavaScriptSerializer();

            var snapshot = serializer.Serialize(new
            {
                quote.Id,
                quote.LeadId,
                quote.VersionNo,
                quote.SubTotal,
                quote.Discount,
                quote.Tax,
                quote.TotalAmount,
                quote.Status,
                SnapshotDate = DateTime.Now
            });

            var historyRecord = new QuoteHistory
            {
                QuoteId = quote.Id,
                VersionNo = quote.VersionNo,
                QuoteData = snapshot
            };

            db.QuoteHistories.Add(historyRecord);
            db.SaveChanges();
        }
    }

    public class QuoteDto
    {
        public int LeadId { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Discount { get; set; }
        public decimal Tax { get; set; }
    }

    public class ApproveQuoteDto
    {
        public int ApprovedByUserId { get; set; }
    }

    public class RejectQuoteDto
    {
        public string Reason { get; set; }
    }
}