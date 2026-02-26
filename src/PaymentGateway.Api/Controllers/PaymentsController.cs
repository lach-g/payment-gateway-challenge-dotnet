using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly IPaymentsRepository _paymentsRepository;

    public PaymentsController(IPaymentsRepository paymentsRepository)
    {
        _paymentsRepository = paymentsRepository;
    }

    [HttpGet("{id:guid}")]
    public ActionResult<PostPaymentResponse> GetPaymentAsync(Guid id)
    {
        if (id == Guid.Empty) 
        {
            return BadRequest(new ErrorResponse { Message = "Payment ID cannot be empty." });
        }

        if (!_paymentsRepository.TryGet(id, out var payment))
        {
            return NotFound(new ErrorResponse { Message = $"Payment with ID {id} not found." });
        }

        return new OkObjectResult(payment);
    }
}