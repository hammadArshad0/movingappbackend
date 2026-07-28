using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Xml.Linq;

namespace task_full_stack.Controllers.documents
{
    public class DocumentController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // Allowed file types & size limit
        private readonly string[] _allowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".docx", ".xlsx" };
        private readonly long _maxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        // GET api/Document?moveId=1
        [HttpGet]
        [Route("api/Document")]
        public IHttpActionResult GetAll(int moveId)
        {
            try
            {
                var documents = db.Documents
                                   .Where(d => d.MoveId == moveId)
                                   .OrderByDescending(d => d.UploadedDate)
                                   .Select(d => new
                                   {
                                       d.Id,
                                       d.FileName,
                                       d.FileType,
                                       d.UploadedDate,
                                       DownloadUrl = "/api/Document/download/" + d.Id
                                   })
                                   .ToList();

                return Ok(documents);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Document/upload?moveId=1
        // multipart/form-data file upload
        [HttpPost]
        [Route("api/Document/upload")]
        public async Task<IHttpActionResult> Upload(int moveId)
        {
            try
            {
                if (!Request.Content.IsMimeMultipartContent())
                    return BadRequest("Unsupported media type. Use multipart/form-data.");

                var moveExists = db.Moves.Any(m => m.Id == moveId);
                if (!moveExists)
                    return BadRequest("Invalid MoveId.");

                // Storage folder - App_Data ke andar move-wise organize
                var uploadFolder = HttpContext.Current.Server.MapPath("~/App_Data/Uploads/Move_" + moveId);
                if (!Directory.Exists(uploadFolder))
                    Directory.CreateDirectory(uploadFolder);

                var provider = new MultipartFormDataStreamProvider(uploadFolder);
                await Request.Content.ReadAsMultipartAsync(provider);

                if (!provider.FileData.Any())
                    return BadRequest("No file was uploaded.");

                var savedDocuments = new System.Collections.Generic.List<object>();

                foreach (var file in provider.FileData)
                {
                    var originalFileName = file.Headers.ContentDisposition.FileName?.Trim('"');
                    var extension = Path.GetExtension(originalFileName)?.ToLower();

                    if (!_allowedExtensions.Contains(extension))
                    {
                        File.Delete(file.LocalFileName);
                        return BadRequest($"File type '{extension}' is not allowed.");
                    }

                    var fileInfo = new FileInfo(file.LocalFileName);
                    if (fileInfo.Length > _maxFileSizeBytes)
                    {
                        File.Delete(file.LocalFileName);
                        return BadRequest("File size exceeds the 10 MB limit.");
                    }

                    // Uploaded temp file ko original naam se rename karo (unique prefix ke sath)
                    var uniqueFileName = Guid.NewGuid().ToString("N") + "_" + originalFileName;
                    var finalPath = Path.Combine(uploadFolder, uniqueFileName);
                    File.Move(file.LocalFileName, finalPath);

                    var document = new Document
                    {
                        MoveId = moveId,
                        FileName = originalFileName,
                        FilePath = finalPath,
                        FileType = extension,
                        UploadedDate = DateTime.Now
                    };

                    db.Documents.Add(document);
                    db.SaveChanges();

                    savedDocuments.Add(new
                    {
                        document.Id,
                        document.FileName,
                        document.FileType,
                        document.UploadedDate
                    });
                }

                return Ok(new { message = "File(s) uploaded successfully.", files = savedDocuments });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Document/download/5
        [HttpGet]
        [Route("api/Document/download/{id:int}")]
        public HttpResponseMessage Download(int id)
        {
            var document = db.Documents.FirstOrDefault(d => d.Id == id);

            if (document == null || !File.Exists(document.FilePath))
            {
                var notFoundResponse = Request.CreateResponse(HttpStatusCode.NotFound);
                return notFoundResponse;
            }

            var fileBytes = File.ReadAllBytes(document.FilePath);
            var response = Request.CreateResponse(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(fileBytes);
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
            {
                FileName = document.FileName
            };

            return response;
        }

        // DELETE api/Document/5
        [HttpDelete]
        [Route("api/Document/{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var document = db.Documents.FirstOrDefault(d => d.Id == id);

                if (document == null)
                    return NotFound();

                if (File.Exists(document.FilePath))
                    File.Delete(document.FilePath);

                db.Documents.Remove(document);
                db.SaveChanges();

                return Ok(new { message = "Document deleted successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}