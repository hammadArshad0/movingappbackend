using System;
using System.Linq;
using System.Web.Http;

namespace task_full_stack.Controllers.audit
{
    public class AuditLogController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/AuditLog?tableName=Quotes&userId=5&fromDate=...&toDate=...&pageNumber=1&pageSize=20
        [HttpGet]
        [Route("api/AuditLog")]
        public IHttpActionResult GetAll(string tableName = null, int? userId = null,
                                         DateTime? fromDate = null, DateTime? toDate = null,
                                         int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                var query = db.AuditLogs.AsQueryable();

                if (!string.IsNullOrWhiteSpace(tableName))
                    query = query.Where(a => a.TableName == tableName);

                if (userId.HasValue)
                    query = query.Where(a => a.UserId == userId.Value);

                if (fromDate.HasValue)
                    query = query.Where(a => a.CreatedAt >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(a => a.CreatedAt <= toDate.Value);

                var totalCount = query.Count();

                var logs = query
                            .OrderByDescending(a => a.CreatedAt)
                            .Skip((pageNumber - 1) * pageSize)
                            .Take(pageSize)
                            .Select(a => new
                            {
                                a.Id,
                                a.UserId,
                                ChangedByName = db.Users.Where(u => u.Id == a.UserId)
                                                .Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                                a.TableName,
                                a.ActionType,
                                a.OldValues,
                                a.NewValues,
                                a.CreatedAt
                            })
                            .ToList();

                return Ok(new
                {
                    totalCount,
                    pageNumber,
                    pageSize,
                    data = logs
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/AuditLog/5
        [HttpGet]
        [Route("api/AuditLog/{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var log = db.AuditLogs.FirstOrDefault(a => a.Id == id);

                if (log == null)
                    return NotFound();

                return Ok(log);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/AuditLog/record-history?tableName=Quotes&recordId=10
        // Note: Since OldValues/NewValues are stored as JSON, we match by scanning text.
        // Better long-term design: add a RecordId column to AuditLogs for direct filtering.
        [HttpGet]
        [Route("api/AuditLog/record-history")]
        public IHttpActionResult GetRecordHistory(string tableName, int recordId)
        {
            try
            {
                var logs = db.AuditLogs
                              .Where(a => a.TableName == tableName)
                              .OrderByDescending(a => a.CreatedAt)
                              .ToList()
                              .Where(a => (a.NewValues != null && a.NewValues.Contains("\"Id\":" + recordId))
                                          || (a.OldValues != null && a.OldValues.Contains("\"Id\":" + recordId)))
                              .Select(a => new
                              {
                                  a.Id,
                                  a.ActionType,
                                  a.OldValues,
                                  a.NewValues,
                                  a.CreatedAt
                              })
                              .ToList();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // AuditLogs kabhi manually insert/update/delete nahi hote via API -
        // ye automatically system ke DB SaveChanges interceptor / repository layer se generate hone chahiye.
        // Isliye is controller mein sirf GET (read-only) endpoints hain.
    }
}