-- -----------------------------------------------------------
-- Changing primairy key in DB so houses can actually be sold
-- -----------------------------------------------------------
ALTER TABLE `housings` 
DROP PRIMARY KEY,
ADD PRIMARY KEY (`id`);
