using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Security;

namespace task_full_stack.Controllers.move
{
    public class MoveController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/Move?customerId=1&status=InProgress&pageNumber=1&pageSize=10
        [HttpGet]
        [Route("api/Move")]
        public IHttpActionResult GetAll(int? customerId = null, string status = null,
                                         int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var query = db.Moves.AsQueryable();

                if (customerId.HasValue)
                    query = query.Where(m => m.CustomerId == customerId.Value);

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(m => m.Status == status);

                var totalCount = query.Count();

                var moves = query
                             .OrderByDescending(m => m.Id)
                             .Skip((pageNumber - 1) * pageSize)
                             .Take(pageSize)
                             .Select(m => new
                             {
                                 m.Id,
                                 m.QuoteId,
                                 m.CustomerId,
                                 m.OriginAddress,
                                 m.DestinationAddress,
                                 m.MoveType,
                                 m.TransportType,
                                 m.PlannedStartDate,
                                 m.PlannedEndDate,
                                 m.ActualStartDate,
                                 m.ActualEndDate,
                                 m.Status
                             })
                             .ToList();

                return Ok(new
                {
                    totalCount,
                    pageNumber,
                    pageSize,
                    data = moves
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Move/5
        [HttpGet]
        [Route("api/Move/{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var move = db.Moves.FirstOrDefault(m => m.Id == id);

                if (move == null)
                    return NotFound();

                return Ok(move);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Move/5/status-history
        // Immutable status timeline
        [HttpGet]
        [Route("api/Move/{id:int}/status-history")]
        public IHttpActionResult GetStatusHistory(int id)
        {
            try
            {
                var history = db.MoveStatusHistories
                                 .Where(h => h.MoveId == id)
                                 .OrderBy(h => h.CreatedAt)
                                 .Select(h => new
                                 {
                                     h.Id,
                                     h.Status,
                                     h.ChangedBy,
                                     ChangedByName = db.Users.Where(u => u.Id == h.ChangedBy)
                                                      .Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                                     h.CreatedAt
                                 })
                                 .ToList();

                return Ok(history);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Move/delayed?hours=48
        // mySQL task: Moves delayed more than 48 hours
        [HttpGet]
        [Route("api/Move/delayed")]
        public IHttpActionResult GetDelayedMoves(int hours = 48)
        {
            try
            {
                var cutoff = DateTime.Now.AddHours(-hours);

                var delayedMoves = db.Moves
                                      .Where(m => m.Status != "Completed"
                                                  && m.Status != "Cancelled"
                                                  && m.PlannedEndDate != null
                                                  && m.PlannedEndDate < cutoff)
                                      .Select(m => new
                                      {
                                          m.Id,
                                          m.CustomerId,
                                          m.PlannedEndDate,
                                          m.ActualEndDate,
                                          m.Status,
                                          DelayHours = DbFunctions.DiffHours(m.PlannedEndDate, DateTime.Now)
                                      })
                                      .ToList();

                return Ok(delayedMoves);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Move
        // MANDATORY RULE: Move sirf tab ban sakta hai jab uski Quote "Approved" ho
        [HttpPost]
        [Route("api/Move")]
        public IHttpActionResult Create([FromBody] MoveDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var quote = db.Quotes.FirstOrDefault(q => q.Id == model.QuoteId);

                if (quote == null)
                    return BadRequest("Invalid QuoteId.");

                // === MANDATORY BUSINESS RULE ===
                if (quote.Status != "Approved")
                    return BadRequest("A move cannot be created without an approved quotation.");

                var customerExists = db.Customers.Any(c => c.Id == model.CustomerId);
                if (!customerExists)
                    return BadRequest("Invalid CustomerId.");

                var move = new Move
                {
                    QuoteId = model.QuoteId,
                    CustomerId = model.CustomerId,
                    OriginAddress = model.OriginAddress,
                    DestinationAddress = model.DestinationAddress,
                    MoveType = model.MoveType,
                    TransportType = model.TransportType,
                    PlannedStartDate = model.PlannedStartDate,
                    PlannedEndDate = model.PlannedEndDate,
                    Status = "Scheduled"
                };

                db.Moves.Add(move);
                db.SaveChanges();

                // Pehli status history entry (immutable log ki shuruaat)
                AddStatusHistory(move.Id, "Scheduled", model.CreatedByUserId);

                return Ok(new { message = "Move created successfully.", id = move.Id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Move/5
        // General details update - status yahan se change nahi hota (alag endpoint hai)
        [HttpPut]
        [Route("api/Move/{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] MoveDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var move = db.Moves.FirstOrDefault(m => m.Id == id);

                if (move == null)
                    return NotFound();

                if (move.Status == "Completed" || move.Status == "Cancelled")
                    return BadRequest("Cannot update a move that is already Completed or Cancelled.");

                move.OriginAddress = model.OriginAddress;
                move.DestinationAddress = model.DestinationAddress;
                move.MoveType = model.MoveType;
                move.TransportType = model.TransportType;
                move.PlannedStartDate = model.PlannedStartDate;
                move.PlannedEndDate = model.PlannedEndDate;

                db.SaveChanges();

                return Ok(new { message = "Move updated successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Move/5/status
        // Status change karna -> immutable history mein new row insert (update/delete nahi)
        [HttpPut]
        [Route("api/Move/{id:int}/status")]
        public IHttpActionResult ChangeStatus(int id, [FromBody] ChangeMoveStatusDto model)
        {
            try
            {
                var move = db.Moves.FirstOrDefault(m => m.Id == id);

                if (move == null)
                    return NotFound();

                if (move.Status == "Completed" || move.Status == "Cancelled")
                    return BadRequest("Move status cannot be changed once Completed or Cancelled.");

                move.Status = model.NewStatus;

                if (model.NewStatus == "InProgress" && move.ActualStartDate == null)
                    move.ActualStartDate = DateTime.Now;

                if (model.NewStatus == "Completed")
                    move.ActualEndDate = DateTime.Now;

                db.SaveChanges();

                // Immutable log - kabhi update/delete nahi hoga, sirf insert
                AddStatusHistory(id, model.NewStatus, model.ChangedByUserId);

                return Ok(new { message = "Move status updated successfully.", status = move.Status });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/Move/5
        [HttpDelete]
        [Route("api/Move/{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var move = db.Moves.FirstOrDefault(m => m.Id == id);

                if (move == null)
                    return NotFound();

                if (move.Status != "Scheduled")
                    return BadRequest("Only moves in 'Scheduled' status can be deleted.");

                var hasInvoice = db.Invoices.Any(i => i.MoveId == id);
                if (hasInvoice)
                    return BadRequest("Cannot delete move with existing invoices.");

                var history = db.MoveStatusHistories.Where(h => h.MoveId == id).ToList();
                db.MoveStatusHistories.RemoveRange(history);

                db.Moves.Remove(move);
                db.SaveChanges();

                return Ok(new { message = "Move deleted successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // ================= HELPERS =================

        private void AddStatusHistory(int moveId, string status, int? changedByUserId)
        {
            var record = new MoveStatusHistory
            {
                MoveId = moveId,
                Status = status,
                ChangedBy = changedByUserId,
                CreatedAt = DateTime.Now
            };

            db.MoveStatusHistories.Add(record);
            db.SaveChanges();
        }
    }

    public class MoveDto
    {
        public int QuoteId { get; set; }
        public int CustomerId { get; set; }
        public string OriginAddress { get; set; }
        public string DestinationAddress { get; set; }
        public string MoveType { get; set; }
        public string TransportType { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public int? CreatedByUserId { get; set; }
    }

    public class ChangeMoveStatusDto
    {
        public string NewStatus { get; set; }
        public int? ChangedByUserId { get; set; }
    }
}