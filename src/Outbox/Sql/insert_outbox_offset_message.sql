CREATE OR REPLACE PROCEDURE outbox.insert_outbox_offset_message(topic varchar, partition int, type varchar, key varchar, payload jsonb, headers jsonb)
LANGUAGE plpgsql
AS $$
DECLARE
counter BIGINT;
    tid TID;
BEGIN
insert into outbox.outbox_offset_messages(topic, partition, type, key, payload, headers, "offset")
values (topic, partition, type, key, payload, headers, 0)
    returning ctid into tid;
update outbox.outbox_offsets set value = value + 1 returning value into counter;
update outbox.outbox_offset_messages set "offset" = counter where ctid = tid;

END;
$$;