ALTER TABLE `aaemu_game`.`doodads` 
ADD COLUMN `item_id` BIGINT(20) UNSIGNED NOT NULL AFTER `rotation_z ,
ADD COLUMN `house_db_id` INT(11) UNSIGNED NOT NULL AFTER `item_id`;
