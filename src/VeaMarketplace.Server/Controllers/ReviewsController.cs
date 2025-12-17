using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VeaMarketplace.Server.Hubs;
using VeaMarketplace.Server.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly ReviewService _reviewService;
    private readonly AuthService _authService;
    private readonly NotificationService _notificationService;
    private readonly IHubContext<ContentHub> _contentHub;

    public ReviewsController(
        ReviewService reviewService,
        AuthService authService,
        NotificationService notificationService,
        IHubContext<ContentHub> contentHub)
    {
        _reviewService = reviewService;
        _authService = authService;
        _notificationService = notificationService;
        _contentHub = contentHub;
    }

    [HttpGet("product/{productId}")]
    public ActionResult<ProductReviewListDto> GetProductReviews(
        string productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null)
    {
        var reviews = _reviewService.GetProductReviews(productId, page, pageSize, sortBy);
        return Ok(reviews);
    }

    [HttpPost]
    public async Task<ActionResult<ProductReviewDto>> CreateReview(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] CreateReviewRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (request.Rating < 1 || request.Rating > 5)
            return BadRequest("Rating must be between 1 and 5");

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Review content is required");

        var review = _reviewService.CreateReview(user.Id, request);
        if (review == null)
            return BadRequest("Unable to create review. You may have already reviewed this product.");

        // Notify the seller
        var product = _authService.GetUserById(user.Id); // Get product info through service
        // Broadcast review event
        await _contentHub.Clients.All.SendAsync("NewReview", new
        {
            ProductId = request.ProductId,
            ReviewId = review.Id,
            Username = user.Username,
            Rating = request.Rating
        });

        return Ok(review);
    }

    [HttpPost("{reviewId}/helpful")]
    public ActionResult MarkHelpful(
        string reviewId,
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromQuery] bool isHelpful = true)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (_reviewService.MarkReviewHelpful(reviewId, user.Id, isHelpful))
            return Ok(new { Success = true });

        return NotFound();
    }

    [HttpPost("{reviewId}/report")]
    public ActionResult ReportReview(
        string reviewId,
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] ReportReviewRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (_reviewService.ReportReview(reviewId, user.Id, request.Reason))
            return Ok(new { Success = true, Message = "Review reported successfully" });

        return NotFound();
    }

    [HttpPost("{reviewId}/respond")]
    public ActionResult<ProductReviewDto> AddSellerResponse(
        string reviewId,
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] SellerResponseRequest request)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (_reviewService.AddSellerResponse(reviewId, user.Id, request.Response))
            return Ok(new { Success = true });

        return BadRequest("Unable to add response. You may not be the seller of this product.");
    }

    [HttpDelete("{reviewId}")]
    public ActionResult DeleteReview(
        string reviewId,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var user = GetUserFromToken(authorization);
        if (user == null)
            return Unauthorized();

        if (_reviewService.DeleteReview(reviewId, user.Id))
            return Ok(new { Success = true });

        return NotFound();
    }

    private Shared.Models.User? GetUserFromToken(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            return null;

        var token = authorization["Bearer ".Length..];
        return _authService.ValidateToken(token);
    }
}

public class ReportReviewRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class SellerResponseRequest
{
    public string Response { get; set; } = string.Empty;
}
