Assuming:
- Paymount amount must be > 0 and >= int.MaxValue
- Accept USD, GBP, and AUD
- card is valid to the end of the month in which the expiry date is set to
- we store our own id for each authorized/declined payment, not using the auth code of the bank (decline do not get auth codes)