using Microsoft.AspNetCore.Mvc;
using HtmlAgilityPack;
using System.Net.Http.Json;
using System.Globalization;
using SCOA.Models;
using BLL;

namespace SCOA.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NewsController : ControllerBase
    {
        private readonly BLLService _newsService;
        private readonly AIService _aiService; 

        public NewsController(BLLService newsService, AIService aiService)
        {
            _newsService = newsService;
            _aiService = aiService;
        }

        [HttpGet("recommendations/{userId}")]
        public async Task<IActionResult> GetRecommendations(string userId)
        {
            var articles = await _newsService.GetRecommendationsForUser(userId);

            return Ok(articles ?? new List<Article>());
        }

        [HttpPost("scrape")]
        public async Task<IActionResult> ScrapeNews()
        {
            var articles = await _newsService.GetScrapedNews();
            if (articles == null || !articles.Any())
                return NotFound("לא נמצאו כתבות.");
            return Ok(articles);
        }
        [HttpGet("getAll")]
        public IActionResult GetAll()
        {
            var articles = _newsService.GetRecentArticles();
            return Ok(articles);
        }

        [HttpPost("click")]
        public async Task<IActionResult> TrackClick([FromBody] UserClickDto request)
        {
            if (request == null || string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.ArticleId))
            {
                return BadRequest("User ID and Article ID are required.");
            }

            try
            {
                await _newsService.HandleUserClick(request.UserId, request.ArticleId);
                return NoContent(); 
            }
            catch (Exception ex)
            {
 
                return StatusCode(500, "An error occurred while processing the click.");
            }
        }

        [HttpGet("click_from_mail")]
        public async Task<IActionResult> TrackMailClick([FromQuery] string userHash, [FromQuery] string articleId)
        {
            if (string.IsNullOrEmpty(userHash) || string.IsNullOrEmpty(articleId))
            {
                return BadRequest("User Hash and Article ID are required.");
            }

            if (!string.IsNullOrEmpty(userHash))
            {
                userHash = userHash.Replace(" ", "+");
            }

            try
            {
                string redirectUrl = await _newsService.HandleMailClick(userHash, articleId);

                if (string.IsNullOrEmpty(redirectUrl))
                {
                    return NotFound("User or Article not found.");
                }

                return Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An error occurred while processing the mail click.");
            }
        }


    }    
}