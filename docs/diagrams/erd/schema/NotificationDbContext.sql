CREATE TABLE notification_inbox (
    "Id" uuid NOT NULL,
    "Payload" jsonb NOT NULL,
    "State" character varying(30) NOT NULL,
    "Attempts" integer NOT NULL,
    "NextAttemptAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ErrorCode" character varying(100),
    CONSTRAINT "PK_notification_inbox" PRIMARY KEY ("Id")
);


CREATE TABLE notifications (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    source_event_id uuid,
    recipient_ward_code character varying(50),
    recipient_user_id uuid NOT NULL,
    application_id uuid,
    title character varying(255) NOT NULL,
    content text NOT NULL,
    type character varying(50) NOT NULL,
    channel character varying(30) NOT NULL DEFAULT 'IN_APP',
    is_read boolean NOT NULL DEFAULT FALSE,
    read_at timestamp with time zone,
    action_url character varying(500),
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_notifications" PRIMARY KEY (id),
    CONSTRAINT ck_notifications_channel CHECK (channel IN ('IN_APP','SMS','EMAIL')),
    CONSTRAINT ck_notifications_read CHECK ((is_read AND read_at IS NOT NULL) OR (NOT is_read AND read_at IS NULL)),
    CONSTRAINT ck_notifications_type CHECK (type IN ('APPLICATION_STATUS_CHANGED','INLINE_COMMENT_ADDED','ASSIGNED_OFFICER','SYSTEM_ALERT'))
);


CREATE TABLE notification_deliveries (
    "Id" uuid NOT NULL,
    "NotificationId" uuid NOT NULL,
    "Channel" character varying(30) NOT NULL,
    "State" character varying(30) NOT NULL,
    "Attempts" integer NOT NULL,
    "NextAttemptAt" timestamp with time zone NOT NULL,
    "ErrorCode" character varying(100),
    "SentAt" timestamp with time zone,
    CONSTRAINT "PK_notification_deliveries" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_notification_deliveries_notifications_NotificationId" FOREIGN KEY ("NotificationId") REFERENCES notifications (id) ON DELETE CASCADE
);


CREATE UNIQUE INDEX "IX_notification_deliveries_NotificationId_Channel" ON notification_deliveries ("NotificationId", "Channel");


CREATE INDEX "IX_notification_deliveries_State_NextAttemptAt" ON notification_deliveries ("State", "NextAttemptAt");


CREATE INDEX "IX_notification_inbox_State_NextAttemptAt" ON notification_inbox ("State", "NextAttemptAt");


CREATE INDEX "IX_notifications_created_at" ON notifications (created_at);


CREATE INDEX "IX_notifications_recipient_user_id_created_at_id" ON notifications (recipient_user_id, created_at, id);


CREATE INDEX "IX_notifications_recipient_user_id_is_read" ON notifications (recipient_user_id, is_read);


CREATE UNIQUE INDEX "IX_notifications_source_event_id_recipient_user_id_channel" ON notifications (source_event_id, recipient_user_id, channel);


