CREATE TABLE IF NOT EXISTS short_link (
    id            INT          NOT NULL AUTO_INCREMENT,
    short_url     VARCHAR(255) NOT NULL,
    origin_url    VARCHAR(2048) NOT NULL,
    remark        VARCHAR(500)  NOT NULL DEFAULT '',
    group_name    VARCHAR(128)  NOT NULL DEFAULT '',
    status        INT           NOT NULL DEFAULT 1,
    status_reason VARCHAR(500)  NOT NULL DEFAULT '',
    review_time   TIMESTAMP(0)  NULL DEFAULT NULL,
    create_time   TIMESTAMP(0) NOT NULL,
    update_time   TIMESTAMP(0) NOT NULL,
    access_count  INT          NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_short_link_short_url (short_url),
    KEY ix_short_link_status (status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS short_link_appication (
    app_id      INT          NOT NULL AUTO_INCREMENT,
    app_code    VARCHAR(255) NOT NULL,
    app_name    VARCHAR(255) NOT NULL,
    app_secret  VARCHAR(255) NOT NULL,
    remark      VARCHAR(500) NOT NULL,
    create_time TIMESTAMP(0) NOT NULL,
    update_time TIMESTAMP(0) NOT NULL,
    status      INT          NOT NULL,
    PRIMARY KEY (app_id),
    UNIQUE KEY uq_short_link_application_code (app_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS short_link_log (
    log_id       INT          NOT NULL AUTO_INCREMENT,
    link_id      INT          NOT NULL,
    ip           VARCHAR(255) NOT NULL,
    os_type      INT          NOT NULL,
    browser_type INT          NOT NULL,
    create_time  TIMESTAMP(0) NOT NULL,
    PRIMARY KEY (log_id),
    KEY ix_short_link_log_link_id (link_id),
    KEY ix_short_link_log_create_time (create_time)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
