namespace AAEmu.Game.Models.Game.GameConfigs;

// The latest client reports the following enums (needs verification if actually used)
public enum ContentConfigEnum : uint
{
    LaborPowerPerTick = 1,
    LaborPowerTick = 2,
    DuelDistance = 3,
    DuelWaitResponse = 4,
    // 5
    LimitedMaxLaborPower = 6,
    // 7
    MaxSpecialtyPriceRatio = 8,
    MinSpecialtyPriceRatio = 9,
    MaxSimCount = 10,
    AuctionCharge = 11,
    AuctionDeposit6 = 12,
    AuctionDeposit12 = 13,
    AuctionDeposit24 = 14,
    AuctionDeposit48 = 15,
    QuestLetItDone = 16,
    QuestOverDone = 17,
    AdjustRatioPerTrade = 18,
    RegulateRation = 19,
    RegulateDownTime = 20,
    RegulateUpTime = 21,
    CoinPerGoldRation = 22,
    ActabilityMinDiceRatio = 23,
    // 24
    LpStart = 25,
    LpMax = 26,
    LpUpConsume = 27,
    LpUpValue = 28,
    PcbangSpeedup = 29,
    // 30
    DailyLimit = 31,
    // 32 .. 36
    BaseHouseSalePrice = 37, // ? (is 100g in 1.2)
    ResetAbility = 38,
    ChangeAbility = 39,
    AuctionDepositMax = 40,
    InventoryExpansionMaxSize = 41,
    BankExpansionMaxSize = 42,
    ItemSecureUnlockDelayTime = 43,
    // 44
    FactionDiplomacyRequestCount = 45,
    FactionDiplomacyDenyCount = 46,
    BeautyshopUseItemKind = 47,
    // 48 .. 49
    FactionDeleteDelay = 50,
    FactionDiplomacyHistorySize = 51,
    GenderTransferUseItemKind = 52,
    GenderTransferUseItemCount = 53,
    // 54 .. 56
    SellHouseSealType = 57,
    RatioMod = 59,
    SellerShareRatio = 60,
    TaxGoldRatio = 61,
    SocketingLevelLimit = 62,
    Pcbang = 63,
    MailCoolTime = 64,
}

/*
    --- Likely values for versions newer than 1.2 ---

    65	prepay_house_tax_require_item_type
    66	prepay_house_tax_require_item_count
    67	auction_charge_pcbang_discount
    68	auction_deposit_pcbang_discount
    72	pcbang_ratio
    73	expedition_member_limit
    74	mobilization_order_level
    75	mobilization_order_leadership_point
    76	mobilization_order_ui_open_second
    77	daily_leadership_decrease
    78	leadership_limit_by_honor_point
    79	leadership_offset_percent_by_hornor_point
    80	hand_over_owner_check_time
    81	hand_over_owner_check_count
    82	free_resurrection_limit_cnt
    83	expedition_level_max
    86	item_rnd_attr_activate
    87	sell_backpack_level_limit
    93	use_item_refurbishment
    94	expedition_war_initial_money_for_declaration
    95	expedition_war_money_multiplier
    96	expedition_war_duration
    97	expedition_war_duration_for_protection
    98	mobilization_order_accept_delay
    99	expedition_contribution_point_shop
    100	living_point_shop
    101	honor_point_shop
    102	normal_mail_cost
    103	express_mail_cost
    104	normal_mail_attachment_cost
    105	express_mail_attachment_cost
    106	expedition_recruit_period_min
    107	expedition_recruit_period_max
    108	expedition_recruit_apply_max
    109	mobilization_order_daily_count_max
    110	expedition_recruit_period_min_cost
    111	expedition_recruit_period_max_cost
    112	expedition_summon_item
    113	pcbang_benefit_ui_style
    114	expedition_war_reward_for_win
    115	expedition_war_reward_for_lose
    116	expedition_war_reward_for_draw
    117	mate_revive_hp_percent
    118	mate_revive_mp_percent
    119	mate_revive_delay
    120	swap_ability_set
    121	expedition_rejoin
    122	siege_game_reward_win_item
    123	siege_game_reward_lose_item
    124	ability_set_slot_expand_item_need_cnt1
    125	ability_set_slot_expand_item_need_cnt2
    126	ability_set_slot_expand_item_need_cnt3
    127	unbind_equip_bind_item
    128	package_demolish_seal_type
    129	nation_member_limit
    130	family_max_count
    131	ability_set_free_activation_count
    132	national_tax_in_kind_rate
    133	housing_hostile_faction_tax_add
    135	auction_charge_account_buff_discount
    136	auction_deposit_account_buff_discount
    142	faction_diplomacy_term
    143	currency_exchange_fee
    144	bless_uthstin_base_stats
    145	bless_uthstin_max_stats_limit
    146	bless_uthstin_max_stats_extend_per_point
    151	bless_uthstin_apply_limit_count
    152	trial_sentence_per_crime_point
    153	trial_sentence_pirate_base
    154	trial_sentence_ratio_range_1
    155	trial_sentence_ratio_range_2
    156	trial_sentence_ratio_range_3
    157	trial_sentence_ratio_range_4
    158	trial_sentence_ratio_range_5
    159	trial_sentence_additional_ratio_1
    160	trial_sentence_additional_ratio_2
    161	trial_sentence_additional_ratio_3
    164	default_favorite_portal_limit
    165	resident_system_charge
    166	resident_discount
    168	tax_item_set_type
    169	expedition_rename_period
    170	bless_uthstin_Init_ItemType
    171	bless_uthstin_max_stats_extend_ItemType
    172	bless_uthstin_Init_Item_Num
    174	family_max_level
    175	family_join_leave_item
    176	family_rejoin_delay_time
    177	family_login_inc_exp
    178	family_leave_dec_exp
    179	resident_desc_refresh
    180	resident_member_refresh
    181	quest_let_it_done_money
    182	quest_over_done_money
    183	family_name_change_item
    184	family_name_change_item_count
    185	family_name_change_delay
    186	max_local_laborpower
    187	heir_start_level
    188	tradegoods_on_sell_actability_group
    189	tradegoods_on_sell_actability_limit
    190	tradegoods_on_sell_level_limit
    191	stock_freshness_limit
    192	tradegoods_on_buy_actability_group
    193	tradegoods_on_buy_actability_limit
    194	tradegoods_on_buy_level_limit
    195	tradegoods_stock_limit
    196	specialty_price_tradegoods_count
    197	specialty_price_recover_rate
    198	goods_stock_limit
    199	max_tradegoods_price_ratio
    200	min_tradegoods_price_ratio
    201	adjust_tradegoods_ratio_per_trade
    202	regulate_tradegoods_ratio
    203	tradegoods_regulate_down_time
    204	tradegoods_regulate_up_time
    205	tradegoods_coin_per_gold_ratio
    206	tradegoods_mail_interest
    207	specialty_mail_interest
    208	reset_heir_skill_cost
    209	resident_trade_house_refresh
    210	specialty_goods_ratio_count
    211	battle_field_shop
    212	change_appellation_item
    213	change_appellation_stamp_item
    214	item_secure_money
    215	use_equip_item_secure_ui
    217	mobilization_order_give_item
    218	battle_field_idle_status_seconds
    219	use_second_password_when_ingameshop
    220	use_second_password_when_item
    221	use_second_password_when_money
    222	use_second_password_when_item_lock
    223	use_second_password_when_item_unlock
    224	craft_order_charge_for_resident
    225	use_second_password_when_delete_character
    226	use_second_password_when_etc_action
    227	beautyshop_pcbang_use_item_kind
    229	free_resurrection_max_level
    230	bless_uthstin_select_cost
    231	bless_uthstin_copy_cost
    232	bless_uthstin_expand_page_item_type
    234	bless_uthstin_expand_item_count_for_page_2
    235	bless_uthstin_expand_item_count_for_page_3
    236	equip_slot_reinforce_change_level_effect_item
    237	equip_slot_reinforce_enable_min_level
    238	force_attack_level
    239	max_labor_power_by_item
    240	recharged_lp_efficiency_good
    241	recharged_lp_efficiency_normal
    242	recharged_lp_efficiency_bad
    243	faction_kick_inactive_day
    244	critical_min_ratio_value
    245	item_bind_cannot_equip
    246	evolving_change_charge
    247	evolving_change_charge_max
    248	today_assignment_reset_count
    249	skill_re_enter_time
    250	broadcast_enchant_grade
    251	broadcast_enchant_scale
    252	quest_notifier_limit
    253	today_quest_notifier_limit
    254	combat_ability_max
    255	siege_game_reward_win_owner_item
    256	siege_game_reward_win_hero_item
    257	siege_game_defence_owner_buff
    258	siege_game_defence_hero_buff
    259	siege_game_defence_normal_buff
    260	siege_game_offense_owner_buff
    261	siege_game_offense_hero_buff
    262	siege_game_offense_normal_buff
    263	dominion_system_charge
    264	dominion_desc_refresh
    265	siege_game_reward_win_owner_service_point
    266	siege_game_reward_win_hero_service_point
    267	siege_game_reward_win_member_service_point
    268	siege_second_half_defense_buff
    269	siege_second_half_offense_buff
    270	siege_reward_win_leadship_point_owner
    271	siege_reward_win_leadship_point_hero
    272	siege_reward_win_leadship_point_member
    273	siege_reward_lose_leadship_point_member
    274	return_account_rest_day
    275	return_account_reward_item_type
    276	return_account_reward_block_day
    277	arche_pass_mission_complete_count
    278	arche_pass_mission_change_count
    279	arche_pass_mission_init_count
    280	arche_pass_mission_init_item
    281	item_evolving_material_min_count
    285	siege_defense_win_point
    286	siege_offense_win_point
    287	siege_outlaw_win_point
    288	faction_diplomacy_dialog_timeout
    289	faction_power_siege_maintain
    290	faction_power_raid_maintain
    291	siege_defense_first_revival_doodad_type
    292	siege_defense_second_revival_doodad_type
    293	power_determining_factor_1
    294	power_determining_factor_2
    295	power_determining_factor_3
    296	power_determining_factor_4
    297	power_relation_factor
    298	max_coffers_in_a_house
    299	max_house_decorations
    300	add_friend_list_cooldown
    301	crime_value_for_assault
    302	crime_value_for_theft
    303	crime_value_for_wanted
    304	crime_value_for_unlawful
    305	craft_materials_limit_max
    306	faction_change_quest_cooldown
    307	dropout_hero_comeback_reward_item
    308	faction_inferior_buff
    309	relation_qualifier
    310	power_qualifier
    311	expedition_name_change_ticket
    312	dyeing_ticket
    313	downgrade_intensified_expert_ticket
    314	trade_money_limit
    315	trade_delay_by_target
    316	mail_attachment_money_limit
    317	mail_attachment_delay_by_target
    318	weekly_limit
    319	butler_bind_limit
    320	hero_dominion_daily_limit
    321	hero_dominion_point
    322	hero_dominion_cooldown
    323	hero_dominion_member_limit
    324	hero_dominion_tax_rate_min
    325	hero_dominion_tax_rate_max
    326	butler_actability_reset_limit
    327	butler_production_cost_weekly_charge_amount_limit
    328	butler_production_cost_weekly_free_charge_limit
    329	butler_production_cost_free_charge_amount
    330	housing_trade_tax_rate
    331	power_score_raid_member
    332	power_score_raid_pre_leadership_avg
    333	power_score_raid_leadership_avg
    334	power_score_raid_gear_score_avg
    335	content_roster_save_cool_time
    336	group_mail_attach_money_limit
    337	group_mail_daily_use_count
    338	group_mail_send_cool_time
    339	group_mail_min_size
    340	group_mail_need_lp_point
    341	group_mail_need_period_lp_point
    342	group_mail_exchange_fee
    343	content_roster_min_member_size
    344	butler_specialty_trade_character_level_limit
    345	butler_lp_daily_charge_amount_limit
    347	ability_set_slot_expand_item_need_cnt4
    348	ability_set_slot_expand_item_need_cnt5
    349	siege_outline_buff
    350	special_resurrection_buff
    351	special_resurrection_duration
    352	special_resurrection_cooltime
    353	crime_point_max
    354	archelife_days_to_housing_prepay
    355	tick_recover_local_labor_power
    356	limited_max_local_labor_power
    357	adjust_min_actability_skill_cast_time
    358	daily_honor_war_point_capacity
    359	equip_slot_reinforce_item_level_formula
    360	equip_slot_reinforce_bundle_effect
    361	random_shop_to_link_btn
    362	dominion_tax_limit
    363	visual_race_max_expired_day
    364	all_server_chat_archelife
    365	expedition_summon_role
    366	expedition_today_assignment_reset_count
    367	auto_expedition_change_owner_min_contribution_point_of_candidate
    368	auto_expedition_change_owner_last_logout_hours_of_candidate
    369	auto_expedition_change_owner_last_logout_hours_of_owner
    370	contribution_gold_ratio
    371	contribution_demolish_refund
    372	butler_charge_lp_min
    373	report_badword_user_daily_count
    374	report_badword_user_interval
    375	jury_wait_time
    376	testimony_wait_time
    377	final_testimony_time
    378	sentence_wait_time
    379	instance_refuse_penalty_buff
    380	butler_production_cost_weekly_free_charge_reset_day
    381	arche_pass_reset_weekly_day
    382	weekly_quest_reset_weekly_day
    383	expedition_public_quest_reset_weekly_day
    384	reopen_random_box_favorite_regist_max
    385	max_gear_score_to_recruit
    386	reset_honor_shop_day_of_the_week
 */
