# Chapter 13 · Strategies for Testing and Test-driven Development

[All exercise solutions](README.md)

## 1. Isolated unit tests

Test pricing and quantity rules with ordinary objects. A failing assertion identifies the broken rule quickly. End-to-end tests catch contract and wiring failures, but depend on many services and often report only the final failure. Keep both kinds of tests.

## 2. Plan the stock rule

Write a table for quantities 4, 5 and 6. The expected LowStock values are true, false and false. A change in quantity should immediately change the derived result. This avoids persisting a second, potentially stale flag.

## 3. Red

Name the first test `FourUnitsAreLowStock`. Arrange four units, evaluate the rule and assert true. A stub returning false makes this test fail for the expected reason. Keep the case for five units to catch an accidental inclusive comparison.

## 4. Green

Return `units < 5`. Run the three boundary cases. The executable implementation and tests are in [RuleTests.cs](labs/Tests/RuleTests.cs).

## 5. Refactor

If several product policies use the threshold, extract a named LowStockThreshold constant or a per-category policy object. Keep the same boundary tests throughout. Do not introduce a configurable setting without a business need.

## 6. Test AddItem

[AddItemUpdatesUnitsAndTotal](labs/Tests/RuleTests.cs) starts with an empty basket and adds two units priced at 12.50. It asserts success, two units and a total of 25.00. The second test supplies zero quantity and asserts that the cart stays empty. No database, mock or network call is needed for this rule.

## 7. Ordering integration

[OrderingApiTests](labs/Tests/OrderingApiTests.cs) uses WebApplicationFactory with an isolated SQLite database and a test authentication scheme that issues a known subject. POST `{"productId":1,"quantity":2}` to `/api/orders`. Assert 201, follow Location, and query the database in a fresh scope. Verify BuyerId, quantity, price and an OrderCreated outbox row. Repeat an anonymous POST and expect 401 and no extra rows. For broker compatibility add a separate RabbitMQ integration test. An in-memory message transport cannot test broker acknowledgement behavior.

## 8. Gherkin

[confirmation.feature](labs/Tests/confirmation.feature) gives the initial mailbox state, order event and observable email result. Its redelivery assertion catches a common duplicate-notification bug. Use a test mailbox or fake sender.

## 9. CI test gate

The [GitHub workflow](../.github/workflows/exercise-solutions.yml) builds the solution, runs tests and retains TRX results. In Azure Pipelines, PublishTestResults publishes them to Azure DevOps Test reporting. A change from `< 5` to `<= 5` fails the boundary test for five units. A deployment job depending on a successful test job must then be skipped.

## 10. Declined bank charge

Give Payment an IBankClient dependency and a fake ledger. With Moq, set up `bank.Setup(b => b.AuthorizeAsync(request, ct)).ReturnsAsync(BankResult.Declined)`. Call ProcessPayment once. Assert a Declined result, no captured charge, and one PaymentFailed event or outbox row with the expected order ID. Verify the authorization method was called once and the capture method never ran. Test transient timeouts separately because a decline is a terminal business outcome.
