import pika
import os
import time
import threading
import logging

LOGGER = logging.getLogger(__name__) 
LOGGER.setLevel(logging.INFO)
LOGGER.info("Logger is started")

class TtsPublisher(object):

    def __init__(self, host : str, username : str, password : str):
        self._connection = None
        cred = pika.PlainCredentials(username, password)
        self._connParams = pika.ConnectionParameters(host=host, credentials=cred)
        self._channel = None
        self.queue_name = "tts_ready"
        self.is_running = False

    def run(self):
        if(not self.is_running):
            self._connection = self.connect()
            threading.Thread(target=self._connection.ioloop.start).start()
        
    def reconnect(self):
        self._connecdtion.ioloop.stop()
        time.sleep(3)
        self._connection = self.connect()
        self._connection.ioloop.start()

    def connect(self):
        
        return pika.SelectConnection(self._connParams,
                                    on_open_callback=self.on_open_connection,
                                    on_open_error_callback=self.on_connection_open_error,
                                    on_close_callback=self.on_connection_closed)
    
    def on_connection_open_error(self, _unused_connection, err):
        LOGGER.error(err)
        self.reconnect()
        return

    def on_connection_closed(self, _unused_connection, reason):
        LOGGER.info(reason)
        self.is_running = False
        self.reconnect()

    def on_open_connection(self, conn):
        LOGGER.info("Connection is open")
        self._channel = self._connection.channel(on_open_callback=self.on_channel_open)
    
    def on_channel_open(self, channel):
        self._channel = channel
        channel.queue_declare(queue=self.queue_name)
        self.is_running = True

    def publish_message(self, body, props):
        self._channel.basic_publish(exchange='', routing_key=self.queue_name, body=body, properties=props)
       
    def stop(self):
        self._connection.ioloop.stop()
        self._channel.close()
        self._connection.close()

        

if __name__ == "__main__":
    logging.basicConfig()
    host=os.environ.get("HOST")
    if not host:
        host = "localhost"

    ttsPub = TtsPublisher(host, "guest", "guest") 
    ttsPub.queue_name = "tts"

    target=ttsPub.run()
    time.sleep(2)

    props = pika.BasicProperties()
    props.headers = dict(message_type="ssml", audio_format="weae", message_id="we")

    arr = []
    for i in range(1, 3):  
        t = threading.Thread(target=ttsPub.publish_message, args=["Hello ", props])
        arr.append(t)

    for i in arr:
        i.start()
        i.join()

    ttsPub.stop()
    #time.sleep(5)