namespace HuntRoute;
public enum EmptyKind { None,Loading,NoBills,Completed,Deferred,Unavailable,Filtered }
public sealed record EmptyView(EmptyKind Kind,string Text,string Action){
 public static EmptyView Describe(bool ready,int total,int remaining,int excluded,int unavailable,int shown){
  if(!ready)return new(EmptyKind.Loading,"Данные контрактов загружаются или временно недоступны. Маршрут сохранён.","Обновить данные");
  if(shown>0)return new(EmptyKind.None,"","");
  if(total==0)return new(EmptyKind.NoBills,"Нет взятых контрактов. Выберите дополнение и доску охоты.","Выбрать доску");
  if(remaining==0)return new(EmptyKind.Completed,"Все взятые цели выполнены. Отметки выполнения показаны в сводке.","К сводке и доскам");
  if(excluded>=remaining)return new(EmptyKind.Deferred,$"Доступный обход завершён. Осталось отложенных целей: {remaining}. Выполненными они не считаются.","Показать отложенные");
  if(unavailable+excluded>=remaining)return new(EmptyKind.Unavailable,"Оставшиеся цели отложены или временно недоступны. Причина указана у каждой цели.","Показать причины");
  return new(EmptyKind.Filtered,"Цели есть, но скрыты текущими фильтрами просмотра.","Сбросить фильтры");
 }
}
