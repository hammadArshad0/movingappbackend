using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;

namespace task_full_stack.Controllers.survey
{
    public class SurveyController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        private static readonly string[] AllowedPhotoExtensions =
            { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        // GET api/Survey?leadId=1
        [HttpGet]
        [Route("api/Survey")]
        public IHttpActionResult GetAll(int? leadId = null)
        {
            try
            {
                var query = db.Surveys.AsQueryable();

                if (leadId.HasValue)
                    query = query.Where(s => s.LeadId == leadId.Value);

                var surveys = query
                               .OrderByDescending(s => s.Id)
                               .Select(s => new
                               {
                                   s.Id,
                                   s.LeadId,
                                   s.ScheduledDate,
                                   s.TotalWeight,
                                   s.TotalVolume,
                                   s.Notes
                               })
                               .ToList();

                return Ok(surveys);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Survey/5
        // Survey + Inventory items + Photos, sab ek sath
        [HttpGet]
        [Route("api/Survey/{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var survey = db.Surveys.FirstOrDefault(s => s.Id == id);

                if (survey == null)
                    return NotFound();

                var items = db.InventoryItems
                               .Where(i => i.SurveyId == id)
                               .Select(i => new
                               {
                                   i.Id,
                                   i.RoomId,
                                   RoomName = db.Rooms.Where(r => r.Id == i.RoomId).Select(r => r.Name).FirstOrDefault(),
                                   i.ItemName,
                                   i.Quantity,
                                   i.Weight,
                                   i.Volume,
                                   i.Barcode,
                                   i.Status
                               })
                               .ToList();

                var photos = db.SurveyPhotos
                                .Where(p => p.SurveyId == id)
                                .Select(p => new { p.Id, p.FilePath })
                                .ToList();

                return Ok(new
                {
                    survey.Id,
                    survey.LeadId,
                    survey.ScheduledDate,
                    survey.TotalWeight,
                    survey.TotalVolume,
                    survey.Notes,
                    Items = items,
                    Photos = photos
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Survey
        // Survey schedule karne ke liye
        [HttpPost]
        [Route("api/Survey")]
        public IHttpActionResult Create([FromBody] SurveyDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var leadExists = db.Leads.Any(l => l.Id == model.LeadId);
                if (!leadExists)
                    return BadRequest("Invalid LeadId.");

                var survey = new Survey
                {
                    LeadId = model.LeadId,
                    ScheduledDate = model.ScheduledDate,
                    TotalWeight = 0,
                    TotalVolume = 0,
                    Notes = model.Notes
                };

                db.Surveys.Add(survey);
                db.SaveChanges();

                return Ok(new { message = "Survey scheduled successfully.", id = survey.Id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Survey/5
        [HttpPut]
        [Route("api/Survey/{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] SurveyDto model)
        {
            try
            {
                if (model == null)
                    return BadRequest("Invalid data.");

                var survey = db.Surveys.FirstOrDefault(s => s.Id == id);

                if (survey == null)
                    return NotFound();

                survey.ScheduledDate = model.ScheduledDate;
                survey.Notes = model.Notes;

                db.SaveChanges();

                return Ok(new { message = "Survey updated successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Survey/5/inventory-item
        // Survey mein inventory item add karna
        [HttpPost]
        [Route("api/Survey/{id:int}/inventory-item")]
        public IHttpActionResult AddInventoryItem(int id, [FromBody] InventoryItemDto model)
        {
            try
            {
                var survey = db.Surveys.FirstOrDefault(s => s.Id == id);
                if (survey == null)
                    return NotFound();

                if (string.IsNullOrWhiteSpace(model.ItemName))
                    return BadRequest("ItemName is required.");

                var item = new InventoryItem
                {
                    SurveyId = id,
                    RoomId = model.RoomId,
                    ItemName = model.ItemName,
                    Quantity = model.Quantity,
                    Weight = model.Weight,
                    Volume = model.Volume,
                    Barcode = model.Barcode,
                    Status = "Pending"
                };

                db.InventoryItems.Add(item);

                // Survey ke total weight/volume recalculate karo
                RecalculateSurveyTotals(survey);

                db.SaveChanges();

                return Ok(new { message = "Inventory item added successfully.", id = item.Id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Survey/5/upload-photo
        // Actual image file upload (multipart/form-data, field name = "file")
        // Ye woh endpoint hai jo React frontend "/Survey/{id}/upload-photo" pe call karta hai.
        [HttpPost]
        [Route("api/Survey/{id:int}/upload-photo")]
        public IHttpActionResult UploadPhoto(int id)
        {
            try
            {
                var survey = db.Surveys.FirstOrDefault(s => s.Id == id);
                if (survey == null)
                    return NotFound();

                if (!HttpContext.Current.Request.Files.AllKeys.Contains("file"))
                    return BadRequest("No file uploaded. Expected form field named 'file'.");

                var file = HttpContext.Current.Request.Files["file"];

                if (file == null || file.ContentLength == 0)
                    return BadRequest("Uploaded file is empty.");

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!AllowedPhotoExtensions.Contains(extension))
                    return BadRequest("Only image files (jpg, jpeg, png, gif, webp) are allowed.");

                // 10 MB limit
                if (file.ContentLength > 10 * 1024 * 1024)
                    return BadRequest("File size must not exceed 10 MB.");

                var folderPath = HttpContext.Current.Server.MapPath("~/Uploads/SurveyPhotos");

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var fullPath = Path.Combine(folderPath, uniqueFileName);

                file.SaveAs(fullPath);

                var relativePath = $"/Uploads/SurveyPhotos/{uniqueFileName}";

                var photo = new SurveyPhoto
                {
                    SurveyId = id,
                    FilePath = relativePath
                };

                db.SurveyPhotos.Add(photo);
                db.SaveChanges();

                return Ok(new
                {
                    message = "Photo uploaded successfully.",
                    id = photo.Id,
                    filePath = relativePath
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Survey/5/photo
        // Alternate: photo record add karna jab FilePath pehle se maloom ho
        // (e.g. koi doosra service already file save kar chuka ho)
        [HttpPost]
        [Route("api/Survey/{id:int}/photo")]
        public IHttpActionResult AddPhoto(int id, [FromBody] SurveyPhotoDto model)
        {
            try
            {
                var survey = db.Surveys.FirstOrDefault(s => s.Id == id);

                if (survey == null)
                    return NotFound();

                if (model == null || string.IsNullOrWhiteSpace(model.FilePath))
                    return BadRequest("FilePath is required.");

                var photo = new SurveyPhoto
                {
                    SurveyId = id,
                    FilePath = model.FilePath
                };

                db.SurveyPhotos.Add(photo);
                db.SaveChanges();

                return Ok(new
                {
                    message = "Photo added successfully.",
                    id = photo.Id
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/Survey/5
        [HttpDelete]
        [Route("api/Survey/{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var survey = db.Surveys.FirstOrDefault(s => s.Id == id);

                if (survey == null)
                    return NotFound();

                var hasQuote = db.Quotes.Any(q => q.LeadId == survey.LeadId);
                if (hasQuote)
                    return BadRequest("Cannot delete survey once a quote has been generated for this lead.");

                // Related inventory items & photos pehle remove karo
                var items = db.InventoryItems.Where(i => i.SurveyId == id).ToList();
                db.InventoryItems.RemoveRange(items);

                var photos = db.SurveyPhotos.Where(p => p.SurveyId == id).ToList();

                // Disk se bhi physical files delete karo
                foreach (var photo in photos)
                {
                    var physicalPath = HttpContext.Current.Server.MapPath("~" + photo.FilePath);
                    if (File.Exists(physicalPath))
                        File.Delete(physicalPath);
                }

                db.SurveyPhotos.RemoveRange(photos);

                db.Surveys.Remove(survey);
                db.SaveChanges();

                return Ok(new { message = "Survey deleted successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // ================= HELPERS =================

        private void RecalculateSurveyTotals(Survey survey)
        {
            var items = db.InventoryItems.Where(i => i.SurveyId == survey.Id).ToList();

            survey.TotalWeight = items.Sum(i => (i.Weight ?? 0) * (i.Quantity ?? 1));
            survey.TotalVolume = items.Sum(i => (i.Volume ?? 0) * (i.Quantity ?? 1));
        }
    }

    public class SurveyDto
    {
        public int LeadId { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public string Notes { get; set; }
    }

    public class InventoryItemDto
    {
        public int? RoomId { get; set; }
        public string ItemName { get; set; }
        public int? Quantity { get; set; }
        public decimal? Weight { get; set; }
        public decimal? Volume { get; set; }
        public string Barcode { get; set; }
    }

    public class SurveyPhotoDto
    {
        public string FilePath { get; set; }
    }
}