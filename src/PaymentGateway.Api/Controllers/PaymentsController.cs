using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models.Requests;

using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly ILogger<PaymentsController> _logger;
    private readonly IPaymentService _paymentService;

    public PaymentsController(ILogger<PaymentsController> logger, IPaymentService paymentService)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(paymentService);

        _logger = logger;
        _paymentService = paymentService;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostPaymentResponse>> GetPayment(Guid id)
    {
        if (id == Guid.Empty) 
        {
            _logger.LogWarning("Received request for payment with empty GUID.");
            return BadRequest(new ErrorResponse { Message = "Payment ID cannot be empty." });
        }

        var (found, payment) = await _paymentService.GetAsync(id);

        if (!found)
        {
            _logger.LogWarning("Received request for non-existent payment with ID {Id}.", id);
            return NotFound(new ErrorResponse { Message = $"Payment with ID {id} not found.", KeyValuePairs = new Dictionary<string, string> { { "PaymentId", id.ToString() } } });
        }

        _logger.LogInformation("Responding with payment for ID {Id} successfully.", id);

        return new OkObjectResult(payment);
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync([FromBody] PostPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            _logger.LogWarning("Received null payment request.");
            return BadRequest(new ErrorResponse { Message = "Request body cannot be null." });
        }

        var result = await _paymentService.ProcessAsync(request, cancellationToken);

        switch (result.Outcome)
        {
            case PaymentOutcome.ValidationError:
                return BadRequest(new ErrorResponse { Message = "Validation failed.", KeyValuePairs = result.ValidationErrors });
            case PaymentOutcome.BankUnavailable:
                return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse { Message = "Payment processing is temporarily unavailable. Please try again later.", KeyValuePairs = new Dictionary<string, string> { { "Outcome", result.Outcome.ToString() }, { "ErrorMessage", result.ErrorMessage ?? "N/A" } } });
            case PaymentOutcome.UnexpectedError:
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse { Message = "Payment processing failed. Please try again later.", KeyValuePairs = new Dictionary<string, string> { { "Outcome", result.Outcome.ToString() }, { "ErrorMessage", result.ErrorMessage ?? "N/A" } } });
        }

        _logger.LogInformation("Payment processed successfully for card ending in {CardNumberLastFour}. Payment ID: {PaymentId}, Status: {Status}", request.CardNumber[^4..], result.Payment!.Id, result.Payment.Status);

        return new CreatedAtActionResult(nameof(GetPayment), "Payments", new { id = result.Payment.Id }, result.Payment);
    }
}