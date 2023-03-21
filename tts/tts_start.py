import pika, sys, os
import tts
import time
import threading
from concurrent.futures import ThreadPoolExecutor
import tts_publisher as ttsPub
import logging

LOGGER = logging.getLogger(__name__)


host = os.environ.get("HOST")
if not host:
    host = "localhost"


publisher = None
username = "guest"
password = "guest"

def main():
    executor = ThreadPoolExecutor(10)
    queue_name = "tts"
    (channel, con) = open_channel()
    global publisher 
    if(publisher == None):
        publisher = ttsPub.TtsPublisher(host, username, password)

    publisher.run()

    while not publisher.is_running:
        time.sleep(0.1)

    channel.queue_declare(queue=queue_name)

    def callback(ch, method, properties, body):
        f = executor.submit(process, properties.headers, body)

    def process(properties, body):
        #start = time.time()
        bodyData = body.decode()
        print(bodyData)

        message_type = properties.get("message_type")
        audio_format = properties.get("audio_format")
        message_id = properties.get("message_id")
        
        data = tts.tts(bodyData, message_type, audio_format)
        
        send_ready_WAV(data, audio_format, message_id)
        #print(time.time() - start)

    channel.basic_consume(queue=queue_name, on_message_callback=callback, auto_ack=True)

    LOGGER.info('Waiting for messages.')
    channel.start_consuming()

def send_ready_WAV(data, audio_format, message_id):
    props = pika.BasicProperties()
    if(data == None):
        data = "None"
    props.headers = dict(success=(type(data) == bytes), audio_format=audio_format, message_id=message_id)
    publisher.publish_message(data, props)

def open_channel():
    i = 0
    while i < 3:
        i+=1
        try:
            cred = pika.PlainCredentials(username, password)
            main_connection = pika.BlockingConnection(
                pika.ConnectionParameters(host=host, credentials=cred))
            channel = main_connection.channel() 
            return (channel, main_connection)
        except pika.exceptions.AMQPConnectionError:
            LOGGER.info("Connection error, try reconnect..\n")
            time.sleep(5)

if __name__ == '__main__':
    logging.basicConfig()
    LOGGER.setLevel(logging.INFO)
    LOGGER.info("Broker host - " + host)

    while True:
        try:
            main()
        except KeyboardInterrupt:
            LOGGER.info('Interrupted')
            try:
                sys.exit(0)
            except SystemExit:
                os._exit(0)
        except BaseException as e:
            LOGGER.info(e)

