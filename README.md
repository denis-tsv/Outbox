# Outbox
Different ways to implement Transactional Outbox pattern for PostgreSQL. Each way is in own branch:
1. master - basic implementation (long transaction, one worker, can miss messages). [1-basic-version-advisory-lock](https://github.com/denis-tsv/Outbox/pull/1/files) - advisory lock instead of long transaction
2. [2-lock-messages-for-update](https://github.com/denis-tsv/Outbox/pull/2/files) - lock each message for update (broken messages order, long transaction)
3. [3-pessimistic-lock-messages](https://github.com/denis-tsv/Outbox/pull/3/files) - pessimistic lock each message (removed long transaction)
4. [4-virtual-partitions-transaction-id](https://github.com/denis-tsv/Outbox/pull/4/files) - virtual partitions (read messages to send can stuck because of long transaction)
5. [5-virtual-partition-with-sequence](https://github.com/denis-tsv/Outbox/pull/5/files) - virtual partition with synchronized insert to messages table (write messages can stuck on long transaction)
6. [6-virtual-partition-no-missing-ids](https://github.com/denis-tsv/Outbox/pull/6/files) - virtual partitions without missing numbers for each partition