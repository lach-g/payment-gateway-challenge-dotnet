using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly ILogger<PaymentsController> _logger;
    private readonly IPaymentsRepository _paymentsRepository;

    public PaymentsController(ILogger<PaymentsController> logger, IPaymentsRepository paymentsRepository)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(paymentsRepository);

        _logger = logger;
        _paymentsRepository = paymentsRepository;
    }

    [HttpGet("{id:guid}")]
    public ActionResult<PostPaymentResponse> GetPaymentAsync(Guid id)
    {
        if (id == Guid.Empty) 
        {
            _logger.LogWarning("Received request for payment with empty GUID.");
            return BadRequest(new ErrorResponse { Message = "Payment ID cannot be empty." });
        }

        if (!_paymentsRepository.TryGet(id, out var payment))
        {
            _logger.LogWarning("Received request for non-existent payment with ID {Id}.", id);
            return NotFound(new ErrorResponse { Message = $"Payment with ID {id} not found.", KeyValuePairs = new Dictionary<string, object> { { "PaymentId", id } } });
        }

        _logger.LogInformation("Responding with payment for ID {Id} successfully.", id);

        return new OkObjectResult(payment);
    }
}