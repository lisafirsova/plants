-- Эти запросы показывают данные, которые записывает приложение.
-- В pgAdmin открой plants_db -> Query Tool и выполни файл целиком.

select *
from app_users
order by id;

select p.id,
       p.name as plant_name,
       t.name as plant_type,
       u.email as user_email,
       p.watering_enabled,
       p.fertilizer_enabled
from app_plants p
join app_users u on u.id = p.user_id
left join app_plant_types t on t.id = p.plant_type_id
order by p.id;

select s.id,
       p.name as plant_name,
       s.type as schedule_type,
       s.start_date,
       s.period_value,
       pu.name as period_unit,
       s.notification_time,
       s.last_completed_date
from app_watering_schedules s
join app_plants p on p.id = s.plant_id
left join app_period_units pu on pu.id = s.period_unit_id
order by s.start_date;
