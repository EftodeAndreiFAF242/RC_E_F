// Lab 2 bonus: the same client and provider, in Java.
import java.io.BufferedReader;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.Socket;
import java.nio.charset.StandardCharsets;

public class ShopClient {
    private static final Object SEND_LOCK = new Object();
    private static OutputStream output;
    private static volatile boolean quitting = false;

    public static void main(String[] args) throws Exception {
        if (args.length != 3 || !(args[0].equals("client") || args[0].equals("provider"))) {
            System.out.println("Usage: java ShopClient.java client|provider <address> <port>");
            System.exit(1);
        }

        Socket socket;
        try {
            socket = new Socket(args[1], Integer.parseInt(args[2]));
        } catch (IOException | NumberFormatException e) {
            System.out.println("Cannot connect: " + e.getMessage());
            System.exit(1);
            return;
        }

        socket.setTcpNoDelay(true);
        socket.setSoTimeout(10_000);
        output = socket.getOutputStream();
        System.out.println("Connected as " + args[0] + " (Java). Type quit to leave.");

        startDaemon(() -> receiveLoop(socket));
        startDaemon(ShopClient::heartbeatLoop);
        send("HELLO " + args[0]);

        BufferedReader keyboard = new BufferedReader(new InputStreamReader(System.in));
        String text;
        while ((text = keyboard.readLine()) != null) {
            text = text.trim();
            if (text.equalsIgnoreCase("quit") || text.equalsIgnoreCase("exit")) break;
            if (!text.isEmpty()) send(text);
        }

        quitting = true;
        socket.close();
    }

    private static void startDaemon(Runnable body) {
        Thread thread = new Thread(body);
        thread.setDaemon(true);
        thread.start();
    }

    // TCP is a stream: cut lines at newline.
    private static void receiveLoop(Socket socket) {
        String reason = "the server closed the connection";
        try {
            InputStream input = socket.getInputStream();
            ByteArrayOutputStream line = new ByteArrayOutputStream();
            byte[] buffer = new byte[1024];
            int count;
            while ((count = input.read(buffer)) != -1) {
                for (int i = 0; i < count; i++) {
                    if (buffer[i] != '\n') {
                        line.write(buffer[i]);
                        continue;
                    }
                    String message = line.toString(StandardCharsets.UTF_8);
                    line.reset();
                    if (!message.equals("PONG")) System.out.println(message);
                }
            }
        } catch (IOException e) {
            reason = e.getMessage();
        }

        if (quitting) return;
        System.out.println("The server is gone (" + reason + "). Closing.");
        System.exit(0);
    }

    private static void heartbeatLoop() {
        while (true) {
            try {
                Thread.sleep(1000);
            } catch (InterruptedException e) {
                return;
            }
            send("PING");
        }
    }

    // Two threads send, so one lock.
    private static void send(String line) {
        byte[] data = (line + "\n").getBytes(StandardCharsets.UTF_8);
        try {
            synchronized (SEND_LOCK) {
                output.write(data);
            }
        } catch (IOException e) {
            if (quitting) return;
            System.out.println("The server is gone (" + e.getMessage() + "). Closing.");
            System.exit(0);
        }
    }
}
