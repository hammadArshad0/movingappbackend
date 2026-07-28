using System;
using System.Linq;
using System.Web.Http;

namespace task_full_stack.Controllers.billing
{
    public class InvoiceController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/Invoice?moveId=1
        [HttpGet]
        [Route("api/Invoice")]
        public IHttpActionResult GetAll(int? moveId = null)
        {
            try
            {
                var query = db.Invoices.AsQueryable();

                if (moveId.HasValue)
                    query = query.Where(i => i.MoveId == moveId.Value);

                var invoices = query
                                .OrderByDescending(i => i.CreatedAt)
                                .Select(i => new
                                {
                                    i.Id,
                                    i.MoveId,
                                    i.InvoiceNumber,
                                    i.Amount,
                                    i.Status,
                                    i.CreatedAt,
                                    PaidAmount = db.Payments.Where(p => p.InvoiceId == i.Id).Sum(p => (decimal?)p.Amount) ?? 0
                                })
                                .ToList();

                return Ok(invoices);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Invoice/5
        [HttpGet]
        [Route("api/Invoice/{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var invoice = db.Invoices.FirstOrDefault(i => i.Id == id);

                if (invoice == null)
                    return NotFound();

                var paidAmount = db.Payments.Where(p => p.InvoiceId == id).Sum(p => (decimal?)p.Amount) ?? 0;
                var refundedAmount = (from p in db.Payments
                                      join r in db.Refunds on p.Id equals r.PaymentId
                                      where p.InvoiceId == id
                                      select (decimal?)r.Amount).Sum() ?? 0;

                return Ok(new
                {
                    invoice.Id,
                    invoice.MoveId,
                    invoice.InvoiceNumber,
                    invoice.Amount,
                    invoice.Status,
                    invoice.CreatedAt,
                    PaidAmount = paidAmount,
                    RefundedAmount = refundedAmount,
                    OutstandingBalance = invoice.Amount - paidAmount + refundedAmount
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Invoice/outstanding?companyId=1
        // Sare outstanding invoices (Amount > Paid)
        [HttpGet]
        [Route("api/Invoice/outstanding")]
        public IHttpActionResult GetOutstanding()
        {
            try
            {
                var invoices = db.Invoices
                                  .Select(i => new
                                  {
                                      i.Id,
                                      i.MoveId,
                                      i.InvoiceNumber,
                                      i.Amount,
                                      PaidAmount = db.Payments.Where(p => p.InvoiceId == i.Id).Sum(p => (decimal?)p.Amount) ?? 0
                                  })
                                  .ToList()
                                  .Select(i => new
                                  {
                                      i.Id,
                                      i.MoveId,
                                      i.InvoiceNumber,
                                      i.Amount,
                                      i.PaidAmount,
                                      OutstandingBalance = i.Amount - i.PaidAmount
                                  })
                                  .Where(i => i.OutstandingBalance > 0)
                                  .ToList();

                return Ok(invoices);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Invoice/monthly-revenue?branchId=1&year=2026
        // mySQL task: monthly revenue by branch
        [HttpGet]
        [Route("api/Invoice/monthly-revenue")]
        public IHttpActionResult GetMonthlyRevenue(int branchId, int year)
        {
            try
            {
                var revenue = (from p in db.Payments
                               join i in db.Invoices on p.InvoiceId equals i.Id
                               join m in db.Moves on i.MoveId equals m.Id
                               join c in db.Customers on m.CustomerId equals c.Id
                               join u in db.Users on c.CompanyId equals u.CompanyId
                               where u.BranchId == branchId && p.PaymentDate.Value.Year == year
                               group p by p.PaymentDate.Value.Month into g
                               select new
                               {
                                   Month = g.Key,
                                   TotalRevenue = g.Sum(x => x.Amount)
                               })
                              .OrderBy(x => x.Month)
                              .ToList();

                return Ok(revenue);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Invoice
        [HttpPost]
        [Route("api/Invoice")]
        public IHttpActionResult Create([FromBody] InvoiceDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var move = db.Moves.FirstOrDefault(m => m.Id == model.MoveId);
                if (move == null)
                    return BadRequest("Invalid MoveId.");

                var invoiceNumber = "INV-" + DateTime.Now.ToString("yyyyMMddHHmmss");

                var invoice = new Invoice
                {
                    MoveId = model.MoveId,
                    InvoiceNumber = invoiceNumber,
                    Amount = model.Amount,
                    Status = "Unpaid",
                    CreatedAt = DateTime.Now
                };

                db.Invoices.Add(invoice);
                db.SaveChanges();

                return Ok(new { message = "Invoice created successfully.", id = invoice.Id, invoiceNumber });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/Invoice/5
        [HttpDelete]
        [Route("api/Invoice/{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var invoice = db.Invoices.FirstOrDefault(i => i.Id == id);

                if (invoice == null)
                    return NotFound();

                var hasPayments = db.Payments.Any(p => p.InvoiceId == id);
                if (hasPayments)
                    return BadRequest("Cannot delete invoice with existing payments.");

                db.Invoices.Remove(invoice);
                db.SaveChanges();

                return Ok(new { message = "Invoice deleted successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }


    // ================= PAYMENTS =================
    public class PaymentController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/Payment?invoiceId=1
        [HttpGet]
        [Route("api/Payment")]
        public IHttpActionResult GetAll(int invoiceId)
        {
            try
            {
                var payments = db.Payments
                                  .Where(p => p.InvoiceId == invoiceId)
                                  .OrderByDescending(p => p.PaymentDate)
                                  .Select(p => new
                                  {
                                      p.Id,
                                      p.Amount,
                                      p.PaymentDate,
                                      p.PaymentMethod
                                  })
                                  .ToList();

                return Ok(payments);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Payment
        // Payment record karna + invoice status auto-update
        [HttpPost]
        [Route("api/Payment")]
        public IHttpActionResult Create([FromBody] PaymentDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var invoice = db.Invoices.FirstOrDefault(i => i.Id == model.InvoiceId);
                if (invoice == null)
                    return BadRequest("Invalid InvoiceId.");

                if (model.Amount <= 0)
                    return BadRequest("Payment amount must be greater than zero.");

                var payment = new Payment
                {
                    InvoiceId = model.InvoiceId,
                    Amount = model.Amount,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = model.PaymentMethod
                };

                db.Payments.Add(payment);
                db.SaveChanges();

                // Invoice status auto update karo (Paid / Partially Paid)
                var totalPaid = db.Payments.Where(p => p.InvoiceId == model.InvoiceId).Sum(p => (decimal?)p.Amount) ?? 0;

                invoice.Status = totalPaid >= invoice.Amount ? "Paid" : "Partially Paid";
                db.SaveChanges();

                return Ok(new { message = "Payment recorded successfully.", id = payment.Id, invoiceStatus = invoice.Status });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }


    // ================= REFUNDS =================
    public class RefundController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/Refund?paymentId=1
        [HttpGet]
        [Route("api/Refund")]
        public IHttpActionResult GetAll(int paymentId)
        {
            try
            {
                var refunds = db.Refunds
                                 .Where(r => r.PaymentId == paymentId)
                                 .Select(r => new { r.Id, r.Amount, r.Reason })
                                 .ToList();

                return Ok(refunds);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Refund
        [HttpPost]
        [Route("api/Refund")]
        public IHttpActionResult Create([FromBody] RefundDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var payment = db.Payments.FirstOrDefault(p => p.Id == model.PaymentId);
                if (payment == null)
                    return BadRequest("Invalid PaymentId.");

                var alreadyRefunded = db.Refunds.Where(r => r.PaymentId == model.PaymentId).Sum(r => (decimal?)r.Amount) ?? 0;

                if (model.Amount + alreadyRefunded > payment.Amount)
                    return BadRequest("Refund amount exceeds the original payment amount.");

                var refund = new Refund
                {
                    PaymentId = model.PaymentId,
                    Amount = model.Amount,
                    Reason = model.Reason
                };

                db.Refunds.Add(refund);
                db.SaveChanges();

                // Invoice status ko wapis "Partially Paid" ya "Unpaid" reflect karwao
                var invoice = db.Invoices.FirstOrDefault(i => i.Id == payment.InvoiceId);
                if (invoice != null)
                {
                    var totalPaid = db.Payments.Where(p => p.InvoiceId == invoice.Id).Sum(p => (decimal?)p.Amount) ?? 0;
                    var totalRefunded = (from p in db.Payments
                                         join r in db.Refunds on p.Id equals r.PaymentId
                                         where p.InvoiceId == invoice.Id
                                         select (decimal?)r.Amount).Sum() ?? 0;

                    var netPaid = totalPaid - totalRefunded;

                    invoice.Status = netPaid >= invoice.Amount ? "Paid" : (netPaid > 0 ? "Partially Paid" : "Unpaid");
                    db.SaveChanges();
                }

                return Ok(new { message = "Refund processed successfully.", id = refund.Id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    public class InvoiceDto
    {
        public int MoveId { get; set; }
        public decimal Amount { get; set; }
    }

    public class PaymentDto
    {
        public int InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
    }

    public class RefundDto
    {
        public int PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; }
    }
}