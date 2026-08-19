CREATE TABLE `permanent_upgrade_type` (
	`permanent_upgrade_type_id`	INT	NOT NULL	COMMENT '영구강화유형 PK',
	`upgrade_name`	VARCHAR(100)	NOT NULL	COMMENT '강화명 (공격력/최대체력/이동속도/공격속도/치명타확률)',
	`target_stat`	VARCHAR(50)	NULL	COMMENT '영향 스탯 키',
	`max_level`	INT	NOT NULL	COMMENT '최대 강화 레벨'
);

CREATE TABLE `player_start_setting` (
	`player_id`	INT	NOT NULL	COMMENT '플레이어 PK & FK (1플레이어 1설정)',
	`start_item_id`	INT	NOT NULL	COMMENT '선택한 시작 무기 FK (start_weapon_select 해금 필요)',
	`inherited_item_id`	INT	NULL	COMMENT '거점 사전 강화 후 반입할 무기 FK (pre_enhance 해금 필요)'
);

CREATE TABLE `player_permanent_upgrade` (
	`player_permanent_upgrade_id`	INT	NOT NULL	COMMENT '플레이어영구강화 PK',
	`player_id`	INT	NOT NULL	COMMENT '플레이어 FK',
	`permanent_upgrade_type_id`	INT	NOT NULL	COMMENT '영구 강화 유형 FK',
	`current_level`	INT	NOT NULL	DEFAULT 0	COMMENT '현재 강화 레벨'
);

CREATE TABLE `enemy_type` (
	`enemy_type_id`	INT	NOT NULL	COMMENT '몬스터유형 PK',
	`enemy_name`	VARCHAR(50)	NOT NULL	COMMENT '몬스터명',
	`enemy_class`	VARCHAR(20)	NOT NULL	DEFAULT 'normal'	COMMENT '등급 (normal / elite)',
	`base_hp`	INT	NOT NULL	COMMENT '기본 체력',
	`base_attack`	INT	NOT NULL	COMMENT '기본 공격력',
	`base_move_speed`	DECIMAL(5, 2)	NOT NULL	COMMENT '기본 이동속도',
	`knockback_resist`	DECIMAL(5, 2)	NOT NULL	DEFAULT 0.00	COMMENT '넉백 저항 (0.00~1.00)',
	`hp_scale_factor`	DECIMAL(5, 2)	NOT NULL	DEFAULT 1.00	COMMENT '체력 스케일링 계수 (층수² 곱연산)',
	`atk_scale_factor`	DECIMAL(5, 2)	NOT NULL	DEFAULT 1.00	COMMENT '공격력 스케일링 계수',
	`size`	VARCHAR(20)	NULL	COMMENT '크기 (small / medium / large) – 엘리트는 large'
);

CREATE TABLE `expedition_resource` (
	`expedition_resource_id`	INT	NOT NULL	COMMENT '탐험보유자원 PK',
	`expedition_id`	INT	NOT NULL	COMMENT '탐험 FK',
	`resource_type_id`	INT	NOT NULL	COMMENT '자원 유형 FK',
	`quantity`	INT	NOT NULL	COMMENT '현재 보유 수량'
);

CREATE TABLE `item_enhance_milestone` (
	`item_enhance_milestone_id`	INT	NOT NULL	COMMENT '무기강화마일스톤 PK',
	`item_id`	INT	NOT NULL	COMMENT '무기 아이템 FK',
	`milestone_count`	INT	NOT NULL	DEFAULT 5	COMMENT '마일스톤 달성 누적 강화 횟수 (기본 5회)',
	`passive_effect_type`	VARCHAR(50)	NOT NULL	COMMENT '해금 특수 패시브 효과 타입',
	`passive_effect_value`	DECIMAL(10, 2)	NULL	COMMENT '효과 수치',
	`description`	VARCHAR(200)	NULL	COMMENT '패시브 설명'
);

CREATE TABLE `expedition_item` (
	`expedition_item_id`	INT	NOT NULL	COMMENT '탐험보유아이템 PK',
	`expedition_id`	INT	NOT NULL	COMMENT '탐험 FK',
	`item_id`	INT	NOT NULL	COMMENT '아이템 마스터 FK',
	`slot_type`	VARCHAR(10)	NOT NULL	DEFAULT 'sub'	COMMENT '슬롯 유형 (main / sub, 주무기 1 + 보조 4)',
	`current_level`	INT	NULL	DEFAULT 0	COMMENT '총 강화 레벨 (5개 자원 강화 횟수 합산)',
	`acquire_order`	INT	NULL	COMMENT '런 내 획득 순서',
	`enhance_wood`	INT	NULL	DEFAULT 0	COMMENT '목재 강화 횟수 → 공격속도(쿨타임)↑',
	`enhance_iron`	INT	NULL	DEFAULT 0	COMMENT '철 강화 횟수 → 공격력↑',
	`enhance_copper`	INT	NULL	DEFAULT 0	COMMENT '구리 강화 횟수 → 공격범위/투사체크기↑',
	`enhance_chemical`	INT	NULL	DEFAULT 0	COMMENT '화학물질 강화 횟수 → 치명타확률↑',
	`enhance_oil`	INT	NULL	DEFAULT 0	COMMENT '기름 강화 횟수 → 이동속도버프↑'
);

CREATE TABLE `resource_type` (
	`resource_type_id`	INT	NOT NULL	COMMENT '자원유형 PK',
	`resource_name`	VARCHAR(50)	NOT NULL	COMMENT '자원명 (목재/철/구리/화학물질/기름/희귀자원)',
	`resource_grade`	VARCHAR(20)	NOT NULL	COMMENT '등급 (normal / rare)'
);

CREATE TABLE `item_type` (
	`item_type_id`	INT	NOT NULL	COMMENT '아이템유형 PK',
	`item_type_name`	VARCHAR(30)	NOT NULL	COMMENT '타입명 (weapon / potion / etc)'
);

CREATE TABLE `expedition_reward_choice` (
	`expedition_reward_choice_id`	INT	NOT NULL	COMMENT '탐험포탈선택 PK',
	`expedition_id`	INT	NOT NULL	COMMENT '탐험 FK',
	`stage_id`	INT	NOT NULL	COMMENT '보상 선택 시점 스테이지 FK',
	`reward_group_id`	INT	NOT NULL	COMMENT '보상 그룹 FK',
	`portal_no`	INT	NOT NULL	COMMENT '포탈 번호 (1~3, 3지선다 무작위 배정)',
	`selected_yn`	CHAR(1)	NULL	DEFAULT 'N'	COMMENT '이 포탈을 선택했는지 여부 (선택된 1건만 Y)'
);

CREATE TABLE `stage_enemy` (
	`stage_enemy_id`	INT	NOT NULL	COMMENT '스테이지몬스터 PK',
	`stage_id`	INT	NOT NULL	COMMENT '스테이지 FK',
	`enemy_type_id`	INT	NOT NULL	COMMENT '몬스터 유형 FK',
	`spawn_weight`	DECIMAL(5, 2)	NOT NULL	DEFAULT 1.00	COMMENT '상대적 스폰 가중치'
);

CREATE TABLE `recipe` (
	`recipe_id`	INT	NOT NULL	COMMENT '제작레시피 PK',
	`result_item_id`	INT	NOT NULL	COMMENT '제작 결과 아이템 FK',
	`rare_resource_type_id`	INT	NOT NULL	COMMENT '소모 희귀 자원 유형 FK',
	`rare_resource_amount`	INT	NOT NULL	COMMENT '소모 희귀 자원 수량'
);

CREATE TABLE `stage` (
	`stage_id`	INT	NOT NULL	COMMENT '스테이지 PK',
	`stage_no`	INT	NOT NULL	COMMENT '스테이지 번호 (층수, 5배수에 익스트랙션 포탈 생성)',
	`stage_section`	VARCHAR(20)	NOT NULL	COMMENT '구역명 (숲/광산/공장/오염된호수/모래황무지/NASA/군부대/생각의방)',
	`stage_type`	VARCHAR(20)	NOT NULL	DEFAULT 'normal'	COMMENT '스테이지 유형 (normal / special)',
	`clear_condition_type`	VARCHAR(30)	NOT NULL	COMMENT '클리어 조건 유형 (survive_and_kill)',
	`timer_seconds`	INT	NOT NULL	DEFAULT 180	COMMENT '클리어 필요 생존 시간 (초)',
	`kill_target`	INT	NOT NULL	DEFAULT 0	COMMENT '클리어 필요 킬 수',
	`target_value`	INT	NOT NULL	COMMENT '클리어 조건 범용 목표값 (레거시 호환)',
	`normal_resource_rate`	DECIMAL(5, 2)	NULL	COMMENT '일반 자원 기본 드랍율',
	`rare_resource_rate`	DECIMAL(5, 2)	NULL	COMMENT '희귀 자원 드랍율',
	`death_loss_rate`	DECIMAL(5, 2)	NULL	COMMENT '사망 시 자원 소실율',
	`return_available_yn`	CHAR(1)	NULL	COMMENT '익스트랙션 포탈 생성 가능 여부 (stage_no % 5 = 0)',
	`merchant_spawn_rate`	DECIMAL(5, 2)	NULL	COMMENT '클리어 후 상인 등장 확률'
);

CREATE TABLE `stage_resource_drop` (
	`stage_resource_drop_id`	INT	NOT NULL	COMMENT '스테이지자원드랍 PK',
	`stage_id`	INT	NOT NULL	COMMENT '스테이지 FK',
	`resource_type_id`	INT	NOT NULL	COMMENT '자원 유형 FK',
	`drop_weight`	DECIMAL(5, 2)	NOT NULL	DEFAULT 1.00	COMMENT '드랍 가중치 (스테이지별 자원 편중 표현)'
);

CREATE TABLE `weapon_enhance_stat` (
	`weapon_enhance_stat_id`	INT	NOT NULL	COMMENT '무기강화스탯매핑 PK',
	`resource_type_id`	INT	NOT NULL	COMMENT '소모 자원 유형 FK',
	`stat_name`	VARCHAR(50)	NOT NULL	COMMENT '강화 스탯 (attack_speed / attack_power / range / crit_rate / move_speed)',
	`description`	VARCHAR(200)	NULL	COMMENT '설명 (예: 목재 소모 → 공격속도(쿨타임) 감소)'
);

CREATE TABLE `expedition` (
	`expedition_id`	INT	NOT NULL	COMMENT '탐험런 PK',
	`player_id`	INT	NOT NULL	COMMENT '플레이어 FK',
	`status`	VARCHAR(20)	NULL	COMMENT '런 상태 (in_progress / extracted / dead)',
	`final_stage_id`	INT	NULL	COMMENT '최종 도달 스테이지 FK',
	`returned_yn`	CHAR(1)	NULL	DEFAULT 'N'	COMMENT '익스트랙션 귀환 여부',
	`dead_yn`	CHAR(1)	NULL	DEFAULT 'N'	COMMENT '사망 여부',
	`preserved_rate`	DECIMAL(5, 2)	NULL	COMMENT '자원 보존율 (0.00~1.00, preserve_resource 해금 시 적용)'
);

CREATE TABLE `player` (
	`player_id`	INT	NOT NULL	COMMENT '플레이어 PK',
	`name`	VARCHAR(50)	NOT NULL	COMMENT '플레이어명'
);

CREATE TABLE `expedition_stage_log` (
	`expedition_stage_log_id`	INT	NOT NULL	COMMENT '탐험스테이지로그 PK',
	`expedition_id`	INT	NOT NULL	COMMENT '탐험 FK',
	`stage_id`	INT	NOT NULL	COMMENT '스테이지 FK',
	`weather_type_id`	INT	NULL	COMMENT '발동된 날씨 FK (기믹 없으면 NULL)',
	`cleared_yn`	CHAR(1)	NULL	COMMENT '클리어 여부',
	`kill_count`	INT	NULL	COMMENT '처치 몬스터 수',
	`survive_seconds`	INT	NULL	COMMENT '생존 시간(초)',
	`return_selected_yn`	CHAR(1)	NULL	COMMENT '이 스테이지에서 익스트랙션 포탈 선택 여부'
);

CREATE TABLE `merchant_goods` (
	`merchant_goods_id`	INT	NOT NULL	COMMENT '상인판매목록 PK',
	`stage_id`	INT	NOT NULL	COMMENT '스테이지 FK',
	`sell_type`	VARCHAR(30)	NOT NULL	COMMENT '판매 유형 (item / resource)',
	`sell_item_id`	INT	NULL	COMMENT '판매 아이템 FK (sell_type=item)',
	`sell_resource_type_id`	INT	NULL	COMMENT '판매 자원 유형 FK (sell_type=resource)',
	`sell_quantity`	INT	NULL	COMMENT '판매 수량',
	`cost_resource_type_id`	INT	NOT NULL	COMMENT '구매 비용 자원 유형 FK',
	`cost_amount_min`	INT	NOT NULL	COMMENT '비용 최솟값',
	`cost_amount_max`	INT	NOT NULL	COMMENT '비용 최댓값',
	`probability`	DECIMAL(5, 2)	NULL	COMMENT '해당 상품 등장 확률'
);

CREATE TABLE `reward_group` (
	`reward_group_id`	INT	NOT NULL	COMMENT '보상그룹 PK',
	`reward_group_name`	VARCHAR(100)	NOT NULL	COMMENT '보상 그룹명'
);

CREATE TABLE `weather_type` (
	`weather_type_id`	INT	NOT NULL	COMMENT '날씨유형 PK',
	`weather_name`	VARCHAR(50)	NOT NULL	COMMENT '날씨명 (안개/호우/태풍/벼락/한파)',
	`effect_type`	VARCHAR(50)	NOT NULL	COMMENT '효과 식별자 (vision_reduce / move_speed_penalty / monster_speed_bonus / periodic_damage / attack_speed_penalty)',
	`effect_target`	VARCHAR(20)	NOT NULL	COMMENT '효과 대상 (player / monster)',
	`effect_value`	DECIMAL(5, 2)	NOT NULL	COMMENT '효과 수치 (음수=감소, 양수=증가, 예: -0.20)',
	`trigger_probability`	DECIMAL(5, 2)	NOT NULL	COMMENT '스테이지 진입 시 발동 확률 (0.00~1.00)'
);

CREATE TABLE `player_unlock` (
	`player_unlock_id`	INT	NOT NULL	COMMENT '플레이어해금 PK',
	`player_id`	INT	NOT NULL	COMMENT '플레이어 FK',
	`unlock_type`	VARCHAR(50)	NOT NULL	COMMENT '해금 기능 유형 (preserve_resource / start_weapon_select / pre_enhance / revive / potion_slot)',
	`unlocked_yn`	CHAR(1)	NULL	DEFAULT 'N'	COMMENT '해금 여부 (Y/N)'
);

CREATE TABLE `player_resource` (
	`player_resource_id`	INT	NOT NULL	COMMENT '플레이어보유자원 PK',
	`player_id`	INT	NOT NULL	COMMENT '플레이어 FK',
	`resource_type_id`	INT	NOT NULL	COMMENT '자원 유형 FK',
	`quantity`	INT	NOT NULL	DEFAULT 0	COMMENT '현재 보유 수량 (거점 기본 창에 표시)'
);

CREATE TABLE `reward_group_detail` (
	`reward_group_detail_id`	INT	NOT NULL	COMMENT '보상그룹상세 PK',
	`reward_group_id`	INT	NOT NULL	COMMENT '보상 그룹 FK',
	`reward_type`	VARCHAR(30)	NOT NULL	COMMENT '보상 유형 (resource / item)',
	`resource_type_id`	INT	NULL	COMMENT '자원 유형 FK (reward_type=resource)',
	`item_id`	INT	NULL	COMMENT '아이템 FK (reward_type=item)',
	`quantity`	INT	NULL	COMMENT '보상 수량',
	`probability`	DECIMAL(5, 2)	NULL	COMMENT '해당 보상 확률'
);

CREATE TABLE `item` (
	`item_id`	INT	NOT NULL	COMMENT '아이템 PK',
	`item_type_id`	INT	NOT NULL	COMMENT '아이템 유형 FK',
	`item_name`	VARCHAR(100)	NOT NULL	COMMENT '아이템명',
	`parent_item_id`	INT	NULL	COMMENT '분기 상위 무기 FK (자기참조, NULL이면 기본 무기)',
	`branch_type`	VARCHAR(10)	NULL	COMMENT '분기 유형 (A / B)',
	`start_weapon_yn`	CHAR(1)	NULL	COMMENT '시작 무기 선택 가능 여부 (start_weapon_select 해금 필요)',
	`craftable_yn`	CHAR(1)	NULL	COMMENT '희귀 자원 레시피로 제작 가능 여부'
);

ALTER TABLE `permanent_upgrade_type` ADD CONSTRAINT `PK_PERMANENT_UPGRADE_TYPE` PRIMARY KEY (
	`permanent_upgrade_type_id`
);

ALTER TABLE `player_start_setting` ADD CONSTRAINT `PK_PLAYER_START_SETTING` PRIMARY KEY (
	`player_id`
);

ALTER TABLE `player_permanent_upgrade` ADD CONSTRAINT `PK_PLAYER_PERMANENT_UPGRADE` PRIMARY KEY (
	`player_permanent_upgrade_id`
);

ALTER TABLE `enemy_type` ADD CONSTRAINT `PK_ENEMY_TYPE` PRIMARY KEY (
	`enemy_type_id`
);

ALTER TABLE `expedition_resource` ADD CONSTRAINT `PK_EXPEDITION_RESOURCE` PRIMARY KEY (
	`expedition_resource_id`
);

ALTER TABLE `item_enhance_milestone` ADD CONSTRAINT `PK_ITEM_ENHANCE_MILESTONE` PRIMARY KEY (
	`item_enhance_milestone_id`
);

ALTER TABLE `expedition_item` ADD CONSTRAINT `PK_EXPEDITION_ITEM` PRIMARY KEY (
	`expedition_item_id`
);

ALTER TABLE `resource_type` ADD CONSTRAINT `PK_RESOURCE_TYPE` PRIMARY KEY (
	`resource_type_id`
);

ALTER TABLE `item_type` ADD CONSTRAINT `PK_ITEM_TYPE` PRIMARY KEY (
	`item_type_id`
);

ALTER TABLE `expedition_reward_choice` ADD CONSTRAINT `PK_EXPEDITION_REWARD_CHOICE` PRIMARY KEY (
	`expedition_reward_choice_id`
);

ALTER TABLE `stage_enemy` ADD CONSTRAINT `PK_STAGE_ENEMY` PRIMARY KEY (
	`stage_enemy_id`
);

ALTER TABLE `recipe` ADD CONSTRAINT `PK_RECIPE` PRIMARY KEY (
	`recipe_id`
);

ALTER TABLE `stage` ADD CONSTRAINT `PK_STAGE` PRIMARY KEY (
	`stage_id`
);

ALTER TABLE `stage_resource_drop` ADD CONSTRAINT `PK_STAGE_RESOURCE_DROP` PRIMARY KEY (
	`stage_resource_drop_id`
);

ALTER TABLE `weapon_enhance_stat` ADD CONSTRAINT `PK_WEAPON_ENHANCE_STAT` PRIMARY KEY (
	`weapon_enhance_stat_id`
);

ALTER TABLE `expedition` ADD CONSTRAINT `PK_EXPEDITION` PRIMARY KEY (
	`expedition_id`
);

ALTER TABLE `player` ADD CONSTRAINT `PK_PLAYER` PRIMARY KEY (
	`player_id`
);

ALTER TABLE `expedition_stage_log` ADD CONSTRAINT `PK_EXPEDITION_STAGE_LOG` PRIMARY KEY (
	`expedition_stage_log_id`
);

ALTER TABLE `merchant_goods` ADD CONSTRAINT `PK_MERCHANT_GOODS` PRIMARY KEY (
	`merchant_goods_id`
);

ALTER TABLE `reward_group` ADD CONSTRAINT `PK_REWARD_GROUP` PRIMARY KEY (
	`reward_group_id`
);

ALTER TABLE `weather_type` ADD CONSTRAINT `PK_WEATHER_TYPE` PRIMARY KEY (
	`weather_type_id`
);

ALTER TABLE `player_unlock` ADD CONSTRAINT `PK_PLAYER_UNLOCK` PRIMARY KEY (
	`player_unlock_id`
);

ALTER TABLE `player_resource` ADD CONSTRAINT `PK_PLAYER_RESOURCE` PRIMARY KEY (
	`player_resource_id`
);

ALTER TABLE `reward_group_detail` ADD CONSTRAINT `PK_REWARD_GROUP_DETAIL` PRIMARY KEY (
	`reward_group_detail_id`
);

ALTER TABLE `item` ADD CONSTRAINT `PK_ITEM` PRIMARY KEY (
	`item_id`
);

ALTER TABLE `player_start_setting` ADD CONSTRAINT `FK_player_TO_player_start_setting_1` FOREIGN KEY (
	`player_id`
)
REFERENCES `player` (
	`player_id`
);

