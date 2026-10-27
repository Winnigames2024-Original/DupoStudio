public class Main {
    public static void main(String[] args) {
        System.out.println("Привет из Java!");
        System.out.println("Собрано через DupoLang, модуль Dupo.Java");
        System.out.println();

        int sum = 0;
        for (int i = 1; i <= 10; i++) {
            sum += i;
        }
        System.out.println("Сумма от 1 до 10 = " + sum);

        String[] parts = { "Dupo", "Studio", "Java" };
        System.out.println(String.join(" · ", parts));
    }
}
