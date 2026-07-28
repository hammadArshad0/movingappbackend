using System;
using System.Linq;
using System.Web.Http;
using System.Web.Security;

namespace task_full_stack.Controllers.inventory
{
    public class InventoryController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // GET api/Inventory?surveyId=1&roomId=2&status=Packed
        [HttpGet]
        [Route("api/Inventory")]
        public IHttpActionResult GetAll(int surveyId, int? roomId = null, string status = null)
        {
            try
            {
                var query = db.InventoryItems.Where(i => i.SurveyId == surveyId);

                if (roomId.HasValue)
                    query = query.Where(i => i.RoomId == roomId.Value);

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(i => i.Status == status);

                var items = query
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

                return Ok(items);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Inventory/room-wise?surveyId=1
        // Room-wise grouped inventory
        [HttpGet]
        [Route("api/Inventory/room-wise")]
        public IHttpActionResult GetRoomWise(int surveyId)
        {
            try
            {
                var items = db.InventoryItems
                               .Where(i => i.SurveyId == surveyId)
                               .ToList();

                var grouped = items
                              .GroupBy(i => i.RoomId)
                              .Select(g => new
                              {
                                  RoomId = g.Key,
                                  RoomName = db.Rooms.Where(r => r.Id == g.Key).Select(r => r.Name).FirstOrDefault(),
                                  Items = g.Select(i => new
                                  {
                                      i.Id,
                                      i.ItemName,
                                      i.Quantity,
                                      i.Weight,
                                      i.Volume,
                                      i.Barcode,
                                      i.Status
                                  }).ToList()
                              })
                              .ToList();

                return Ok(grouped);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/Inventory/barcode/5544332211
        // Barcode/QR scan se item find karna
        [HttpGet]
        [Route("api/Inventory/barcode/{barcode}")]
        public IHttpActionResult GetByBarcode(string barcode)
        {
            try
            {
                var item = db.InventoryItems.FirstOrDefault(i => i.Barcode == barcode);

                if (item == null)
                    return NotFound();

                return Ok(item);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Inventory/5/status
        // MANDATORY RULE: Item "Delivered" tab tak mark nahi ho sakta jab tak "Loaded" na ho chuka ho
        [HttpPut]
        [Route("api/Inventory/{id:int}/status")]
        public IHttpActionResult ChangeStatus(int id, [FromBody] ChangeItemStatusDto model)
        {
            try
            {
                var item = db.InventoryItems.FirstOrDefault(i => i.Id == id);

                if (item == null)
                    return NotFound();

                var validStatuses = new[] { "Pending", "Packed", "Loaded", "Delivered" };
                if (!validStatuses.Contains(model.Status))
                    return BadRequest("Invalid status. Allowed values: Pending, Packed, Loaded, Delivered.");

                // === MANDATORY BUSINESS RULE ===
                if (model.Status == "Delivered" && item.Status != "Loaded")
                    return BadRequest("Item cannot be marked as Delivered unless it was previously Loaded.");

                if (model.Status == "Loaded" && item.Status != "Packed")
                    return BadRequest("Item cannot be marked as Loaded unless it was previously Packed.");

                item.Status = model.Status;
                db.SaveChanges();

                return Ok(new { message = "Item status updated successfully.", status = item.Status });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Inventory/bulk-status
        // Multiple items ek sath scan/update karne ke liye (bulk loading scenario)
        [HttpPut]
        [Route("api/Inventory/bulk-status")]
        public IHttpActionResult BulkChangeStatus([FromBody] BulkStatusDto model)
        {
            try
            {
                if (model == null || model.ItemIds == null || !model.ItemIds.Any())
                    return BadRequest("ItemIds are required.");

                var results = new System.Collections.Generic.List<object>();

                foreach (var itemId in model.ItemIds)
                {
                    var item = db.InventoryItems.FirstOrDefault(i => i.Id == itemId);

                    if (item == null)
                    {
                        results.Add(new { itemId, success = false, message = "Not found." });
                        continue;
                    }

                    if (model.Status == "Delivered" && item.Status != "Loaded")
                    {
                        results.Add(new { itemId, success = false, message = "Must be Loaded before Delivered." });
                        continue;
                    }

                    if (model.Status == "Loaded" && item.Status != "Packed")
                    {
                        results.Add(new { itemId, success = false, message = "Must be Packed before Loaded." });
                        continue;
                    }

                    item.Status = model.Status;
                    results.Add(new { itemId, success = true });
                }

                db.SaveChanges();

                return Ok(results);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT api/Inventory/5
        [HttpPut]
        [Route("api/Inventory/{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] UpdateInventoryItemDto model)
        {
            try
            {
                var item = db.InventoryItems.FirstOrDefault(i => i.Id == id);

                if (item == null)
                    return NotFound();

                item.RoomId = model.RoomId;
                item.ItemName = model.ItemName;
                item.Quantity = model.Quantity;
                item.Weight = model.Weight;
                item.Volume = model.Volume;
                item.Barcode = model.Barcode;

                db.SaveChanges();

                return Ok(new { message = "Inventory item updated successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/Inventory/5
        [HttpDelete]
        [Route("api/Inventory/{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var item = db.InventoryItems.FirstOrDefault(i => i.Id == id);

                if (item == null)
                    return NotFound();

                if (item.Status == "Loaded" || item.Status == "Delivered")
                    return BadRequest("Cannot delete an item that has already been Loaded or Delivered.");

                db.InventoryItems.Remove(item);
                db.SaveChanges();

                return Ok(new { message = "Inventory item deleted successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    // ================= ROOMS (lookup) =================
    public class RoomController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        [HttpGet]
        [Route("api/Room")]
        public IHttpActionResult GetAll()
        {
            try
            {
                var rooms = db.Rooms.Select(r => new { r.Id, r.Name }).ToList();
                return Ok(rooms);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost]
        [Route("api/Room")]
        public IHttpActionResult Create([FromBody] RoomDto model)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.Name))
                    return BadRequest("Room Name is required.");

                var room = new Room { Name = model.Name };
                db.Rooms.Add(room);
                db.SaveChanges();

                return Ok(new { message = "Room created successfully.", id = room.Id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    public class ChangeItemStatusDto
    {
        public string Status { get; set; }
    }

    public class BulkStatusDto
    {
        public System.Collections.Generic.List<int> ItemIds { get; set; }
        public string Status { get; set; }
    }

    public class UpdateInventoryItemDto
    {
        public int? RoomId { get; set; }
        public string ItemName { get; set; }
        public int? Quantity { get; set; }
        public decimal? Weight { get; set; }
        public decimal? Volume { get; set; }
        public string Barcode { get; set; }
    }

    public class RoomDto
    {
        public string Name { get; set; }
    }
}