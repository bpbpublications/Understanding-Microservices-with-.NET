Feature: Order confirmation
  Scenario: A customer receives one confirmation for an accepted order
    Given customer Ada has placed order 42 with a verified email address
    And the test mailbox is empty
    When the notification service receives OrderAccepted for order 42
    Then Ada's mailbox contains one confirmation that references order 42
    And a redelivery of the same event does not send another email
