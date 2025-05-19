-- Таблица для хранения изображений вариантов голосования
CREATE TABLE IF NOT EXISTS `poll_option_images` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `poll_id` INT NOT NULL,
  `option_text` VARCHAR(255) NOT NULL,
  `image_data` LONGBLOB NULL,
  `image_description` TEXT NULL,
  PRIMARY KEY (`id`),
  INDEX `fk_poll_option_images_polls_idx` (`poll_id` ASC),
  CONSTRAINT `fk_poll_option_images_polls`
    FOREIGN KEY (`poll_id`)
    REFERENCES `vopros`.`polls` (`id`)
    ON DELETE CASCADE
    ON UPDATE NO ACTION
);

-- Таблица для хранения голосов по каждому варианту
CREATE TABLE IF NOT EXISTS `poll_votes` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `poll_id` INT NOT NULL,
  `option_text` VARCHAR(255) NOT NULL,
  `votes` INT NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `poll_option_unique` (`poll_id` ASC, `option_text` ASC),
  CONSTRAINT `fk_poll_votes_polls`
    FOREIGN KEY (`poll_id`)
    REFERENCES `vopros`.`polls` (`id`)
    ON DELETE CASCADE
    ON UPDATE NO ACTION
);

-- Таблица для хранения информации о том, кто уже проголосовал
CREATE TABLE IF NOT EXISTS `user_votes` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `poll_id` INT NOT NULL,
  `user_id` INT NOT NULL,
  `vote_date` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `user_poll_unique` (`poll_id` ASC, `user_id` ASC),
  CONSTRAINT `fk_user_votes_polls`
    FOREIGN KEY (`poll_id`)
    REFERENCES `vopros`.`polls` (`id`)
    ON DELETE CASCADE
    ON UPDATE NO ACTION
);

-- Убедимся, что в таблице polls есть все необходимые столбцы
ALTER TABLE `polls` 
ADD COLUMN IF NOT EXISTS `created_by` VARCHAR(255) NULL AFTER `password`; 