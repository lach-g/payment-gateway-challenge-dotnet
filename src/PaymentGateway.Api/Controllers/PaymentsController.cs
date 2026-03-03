using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Enums;

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

    /// <summary>
    /// Retrieves a previously processed payment by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the payment to retrieve.</param>
    /// <returns>
    /// 200 OK with payment details if found; 400 Bad Request if id is an empty GUID;
    /// 404 Not Found if no payment with the given ID exists.
    /// </returns>
    [HttpGet("{id:guid}")]
    public ActionResult<PostPaymentResponse> GetPayment(Guid id)
    {
        if (id == Guid.Empty) 
        {
            _logger.LogWarning("Received request for payment with empty GUID.");
            return BadRequest(new ErrorResponse { Message = "Payment ID cannot be empty." });
        }

        var (found, payment) = _paymentService.Get(id);

        if (!found)
        {
            _logger.LogWarning("Received request for non-existent payment with ID {Id}.", id);
            return NotFound(new ErrorResponse { Message = $"Payment with ID {id} not found.", KeyValuePairs = new Dictionary<string, string> { { "PaymentId", id.ToString() } } });
        }

        _logger.LogInformation("Responding with payment for ID {Id} successfully.", id);

        return new OkObjectResult(payment);
    }

    /// <summary>
    /// Submits a new card payment request for processing through the bank.
    /// </summary>
    /// <param name="request">The payment details including card number, expiry, CVV, currency, and amount.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// 201 Created if authorized or declined by the bank; 400 Bad Request if validation fails or the bank rejects the request;
    /// 502 Bad Gateway if the bank is unavailable; 500 Internal Server Error if an unexpected error occurs.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown if an unrecognised <see cref="PaymentOutcome"/> value is returned by the payment service.</exception>
    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync([FromBody] PostPaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _paymentService.ProcessAsync(request, cancellationToken);

        switch (result.Outcome)
        {
            case PaymentOutcome.Authorized or PaymentOutcome.Declined:
                _logger.LogInformation("Returning 201: payment request processed successfully for card ending in {CardNumberLastFour}. Payment ID: {PaymentId}, Status: {Status}", 
                result.Payment!.CardNumberLastFour, 
                result.Payment.Id, 
                result.Payment.Status);
                return new CreatedAtActionResult(nameof(GetPayment), "Payments", new { id = result.Payment.Id }, result.Payment);
            case PaymentOutcome.ValidationError:
                _logger.LogWarning("Returning 400: payment request failed validation.");
                return StatusCode(StatusCodes.Status400BadRequest, new ErrorResponse { Message = "Validation failed.", KeyValuePairs = result.ValidationErrors });
            case PaymentOutcome.BankRejected:
                _logger.LogWarning("Returning 400: bank rejected payment for card ending in {CardNumberLastFour}. Reason: {ErrorMessage}", 
                request.CardNumber[^4..], 
                result.ErrorMessage);
                return StatusCode(StatusCodes.Status400BadRequest, new ErrorResponse { Message = "Payment was rejected by the bank.", KeyValuePairs = new Dictionary<string, string> { { "Outcome", result.Outcome.ToString() }, { "ErrorMessage", result.ErrorMessage ?? "N/A" } } });
            case PaymentOutcome.BankUnavailable:
                _logger.LogError("Returning 502: bank unavailable for card ending in {CardNumberLastFour}. Reason: {ErrorMessage}", request.CardNumber[^4..], result.ErrorMessage);
                return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse { Message = "Payment processing is temporarily unavailable. Please try again later.", KeyValuePairs = new Dictionary<string, string> { { "Outcome", result.Outcome.ToString() }, { "ErrorMessage", result.ErrorMessage ?? "N/A" } } });
            case PaymentOutcome.UnexpectedError:
                _logger.LogError("Returning 500: unexpected error processing payment for card ending in {CardNumberLastFour}. Reason: {ErrorMessage}", request.CardNumber[^4..], result.ErrorMessage);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse { Message = "Payment processing failed. Please try again later.", KeyValuePairs = new Dictionary<string, string> { { "Outcome", result.Outcome.ToString() }, { "ErrorMessage", result.ErrorMessage ?? "N/A" } } });
            default:
                throw new InvalidOperationException($"Unexpected payment outcome: {result.Outcome}");
        }
    }
}