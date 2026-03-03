# Payment Gateway

An ASP.NET Core Web API that acts as a payment gateway, accepting card payment requests from merchants and forwarding them to a bank simulator.

## Running the service

### Prerequisites

- .NET 8.0 to build locally
- Docker Desktop (bank simulator, payment gateway acceptance tests)

### Docker Compose

Start both the bank simulator and the gateway:

```bash
docker compose up --build
```

The gateway is available at `http://localhost:5000/api/v1/payments`.
The bank simulator admin interface is available at `http://localhost:2525`.

The gateway waits for the bank simulator to be healthy before starting.

### Without Docker

Requires .NET 8 SDK and the bank simulator running separately (see `./imposters/bank_simulator.ejs` for the mountebank configuration).

Note the payment gateway acceptance tests depend on Docker.

```bash
dotnet run --project src/PaymentGateway.Api
```

By default the gateway expects the bank at `http://localhost:8080`. Override with:

```bash
BankApi__BaseUrl=http://your-bank-host dotnet run --project src/PaymentGateway.Api
```

## Running the tests

```bash
# Unit and integration tests
dotnet test test/PaymentGateway.Api.Tests.Unit
dotnet test test/PaymentGateway.Api.Tests.Integration

# Acceptance tests — requires Docker to build and run containers
dotnet test test/PaymentGateway.Api.Tests.Acceptance
```

## API

All routes are versioned under `/api/v{version}`. The current version is `v1`.

### POST `/api/v1/payments`

Submit a card payment for processing.

**Request body**

| Field | Type | Description |
|---|---|---|
| `cardNumber` | string | 14–19 digit numeric string |
| `expiryMonth` | int | 1–12 |
| `expiryYear` | int | Full year, e.g. 2027 (combination of expiryMonth and expiryYear must be current month and year or later) |
| `currency` | string | `USD`, `GBP`, or `AUD` |
| `amount` | int | Amount in minor units (pence or cents), must be > 0 |
| `cvv` | string | 3 or 4 digit numeric string |

**Responses**

| Status | Condition |
|---|---|
| 201 Created | Bank authorized or declined the payment. Body is a `PaymentResponse`. |
| 400 Bad Request | Validation failed, or the bank explicitly rejected the request. Body is an `ErrorResponse`. |
| 502 Bad Gateway | Bank is unavailable. Body is an `ErrorResponse`. |
| 500 Internal Server Error | Unexpected error. Body is an `ErrorResponse`. |

**PaymentResponse**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Authorized",
  "cardNumberLastFour": "1234",
  "expiryMonth": 3,
  "expiryYear": 2027,
  "currency": "GBP",
  "amount": 100
}
```

`status` is either `"Authorized"` or `"Declined"`. Both outcomes return 201.

### GET `/api/v1/payments/{id}`

Retrieve a previously processed payment by its gateway-assigned ID.

**Responses**

| Status | Condition |
|---|---|
| 200 OK | Payment found. Body is a `PaymentResponse`. |
| 400 Bad Request | `id` is an empty GUID. |
| 404 Not Found | No payment with that ID exists. |

### ErrorResponse

All error responses are structured:

```json
{
  "message": "Validation failed.",
  "keyValuePairs": {
    "CardNumber": "Card number must be between 14 and 19 digits."
  }
}
```

`keyValuePairs` is present on validation failures (keyed by field name) and on bank errors (containing the outcome error type and error message).

## Design decisions

**Result objects instead of exceptions for expected error paths**

`PaymentService.ProcessAsync` returns a `PaymentResult` containing a `PaymentOutcome` enum. The controller switches on this to decide the HTTP status code. Exceptions are reserved for cases that should not happen, like an unrecognised enum value appearing at a switch statement. This keeps the controller thin and makes the service straightforward to test without needing to catch exceptions.

**Custom validator over a validation framework**

`PaymentValidator` is a plain class that runs each rule in sequence and collects all errors before returning. The validation rules are simple enough to not require an external dependency. All rules are directly visible in one file and easy to unit test.

**BankClient does not throw on known exceptions**

`BankClient.SendPaymentAsync` catches `HttpRequestException` and `JsonException` internally and returns them as `BankResultStatus.UnexpectedError`. Callers always receive a typed `BankResult` and should not need to handle exceptions from the client for the expected exceptions (it does not trap unexpected ones either). The distinction between the bank being unavailable (`503`), explicitly rejecting a request (`400`), and a communication failure is surfaced through the status enum.

**Polly retry on 503 only**

The retry policy retries only on `ServiceUnavailable` responses, with exponential backoff and a maximum of 3 retries. Other failures (400 from the bank, connection errors) are not retried since retrying them would not change the outcome.

**Both authorized and declined payments are stored**

A payment that the bank declines still gets a gateway-assigned ID and is stored in the repository. The purpose is to allow merchants to look up the outcome of any processed payment, including declined ones.

**In-memory storage**

`PaymentsRepository` uses a `ConcurrentDictionary`. There is no persistence layer. Payments are lost on restart.

**URL-based API versioning**

Routes are versioned as `/api/v{version}/...`. The default version is `1.0`, applied when no version is specified in the URL.

**DI lifetimes**

- `PaymentsRepository` is registered as a singleton because the in-memory dictionary must be shared across requests.
- `PaymentValidator` is a singleton because it is safe to share, being stateless.
- `PaymentService` is scoped to provide one instance for the duration of each request.

**Three levels of tests**

- Unit tests cover `PaymentValidator`, `BankClient`, and `PaymentsRepository` in isolation.
- Integration tests cover `PaymentsController` using `WebApplicationFactory` with the bank client replaced by a mock, testing the full request pipeline without network calls.
- Acceptance tests use Testcontainers to build the payment gateway Docker image and run it alongside the real bank simulator, testing the end-to-end flow over HTTP.

## Assumptions

- Amount is in the smallest denomination of the given currency (pence for GBP, cents for USD/AUD). There is no upper bound validation beyond `int.MaxValue`.
- A card is valid until the end of the expiry month. A card expiring in March 2026 is still valid on 31 March 2026.
- The gateway assigns its own UUID to each payment. The bank's authorization code is not surfaced in the response.
- Only USD, GBP, and AUD are accepted currencies.
- Declined payments are stored and retrievable in the same way as authorized ones.