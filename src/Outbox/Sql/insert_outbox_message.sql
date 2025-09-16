CREATE OR REPLACE PROCEDURE outbox.insert_outbox_message(IN ptopic character varying, IN ppartition integer, IN type character varying, IN key character varying, IN payload jsonb, IN headers jsonb)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
counter int;
BEGIN
update outbox.outbox_offset_sequences
set value = value + 1
where topic = ptopic and partition = ppartition
returning value into counter;
insert into outbox.outbox_messages(topic, partition, type, key, payload, headers, number)
values (ptopic, ppartition, type, key, payload, headers, counter);
END;
$procedure$
;
