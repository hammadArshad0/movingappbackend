using System;
using System.Linq;
using System.Web.Http;

namespace task_full_stack.Controllers.dashboard
{
    public class DashboardController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/Dashboard/summary?companyId=1
        // High-level cards: revenue, pending moves, completed moves
        [HttpGet]
        [Route("api/Dashboard/summary")]
        public IHttpActionResult GetSummary(int companyId)
        {
            try
            {
                var moveIds = (from m in db.Moves
                               join c in db.Customers on m.CustomerId equals c.Id
                               where c.CompanyId == companyId
                               select m.Id)
                              .ToList();

                var totalRevenue = (from p in db.Payments
                                    join i in db.Invoices on p.InvoiceId equals i.Id
                                    where moveIds.Contains(i.MoveId ?? 0)
                                    select p.Amount)
                                    .Sum();

                var pendingMoves = db.Moves.Count(m => moveIds.Contains(m.Id)
                                                        && m.Status != "Completed"
                                                        && m.Status != "Cancelled");

                var completedMoves = db.Moves.Count(m => moveIds.Contains(m.Id) && m.Status == "Completed");

                var totalMoves = moveIds.Count;

                return Ok(new
                {
                    TotalRevenue = totalRevenue,
                    PendingMoves = pendingMoves,
                    CompletedMoves = completedMoves,
                    TotalMoves = totalMoves
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Dashboard/revenue-trend?companyId=1&months=6
        // Last N months ka revenue chart data
        [HttpGet]
        [Route("api/Dashboard/revenue-trend")]
        public IHttpActionResult GetRevenueTrend(int companyId, int months = 6)
        {
            try
            {
                var fromDate = DateTime.Now.AddMonths(-months);

                var trend = (from p in db.Payments
                             join i in db.Invoices on p.InvoiceId equals i.Id
                             join m in db.Moves on i.MoveId equals m.Id
                             join c in db.Customers on m.CustomerId equals c.Id
                             where c.CompanyId == companyId && p.PaymentDate >= fromDate
                             group p by new { p.PaymentDate.Value.Year, p.PaymentDate.Value.Month } into g
                             select new
                             {
                                 g.Key.Year,
                                 g.Key.Month,
                                 Revenue = g.Sum(x => x.Amount)
                             })
                            .OrderBy(x => x.Year).ThenBy(x => x.Month)
                            .ToList();

                return Ok(trend);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Dashboard/employee-kpi?companyId=1
        // Salesperson-wise leads converted, moves completed
        [HttpGet]
        [Route("api/Dashboard/employee-kpi")]
        public IHttpActionResult GetEmployeeKpi(int companyId)
        {
            try
            {
                var kpis = db.Users
                              .Where(u => u.CompanyId == companyId && u.IsDeleted == false)
                              .Select(u => new
                              {
                                  u.Id,
                                  Name = u.FirstName + " " + u.LastName,
                                  TotalLeadsAssigned = db.Leads.Count(l => l.AssignedSalesPersonId == u.Id),
                                  QuotesApproved = db.Quotes.Count(q => q.ApprovedBy == u.Id),
                                  MovesHandled = (from a in db.MoveAssignments
                                                  join dr in db.Drivers on a.DriverId equals dr.Id
                                                  where dr.UserId == u.Id
                                                  select a.MoveId).Distinct().Count()
                              })
                              .ToList();

                return Ok(kpis);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Dashboard/vehicle-utilization?companyId=1
        // Har vehicle kitne % time assigned raha (approx, based on assignment count vs total moves)
        [HttpGet]
        [Route("api/Dashboard/vehicle-utilization")]
        public IHttpActionResult GetVehicleUtilization(int companyId)
        {
            try
            {
                var totalMoves = db.Moves.Count();

                var utilization = db.Vehicles
                                     .Where(v => v.CompanyId == companyId)
                                     .Select(v => new
                                     {
                                         v.Id,
                                         v.VehicleNumber,
                                         v.Status,
                                         AssignedMoveCount = db.MoveAssignments.Count(a => a.VehicleId == v.Id),
                                         UtilizationPercent = totalMoves == 0 ? 0
                                             : Math.Round((decimal)db.MoveAssignments.Count(a => a.VehicleId == v.Id) * 100 / totalMoves, 2)
                                     })
                                     .ToList();

                return Ok(utilization);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}