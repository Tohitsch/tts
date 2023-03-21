# %%
import base64
import os
import torch
import time
from concurrent.futures import ThreadPoolExecutor
import readline

sample_rate = 24000
speaker = 'baya'
model = ''

device = torch.device('cpu')
torch.set_num_threads(2)
local_file = 'model.pt'

if not os.path.isfile(local_file):
        torch.hub.download_url_to_file('https://models.silero.ai/models/tts/ru/v3_1_ru.pt',
                                       local_file)

model = torch.package.PackageImporter(
        local_file).load_pickle("tts_models", "model")
model.to(device)

def tts(text, mtype = "text", audio_format = "wav"):
    try:
        
        if(audio_format == "wav"):
            return get_wav(text, mtype)
        elif(audio_format == "pcm"):
            #start = time.time()
            q = get_pcm(text, mtype)
            #print(time.time() - start)
            return q
        
    except ValueError as ve:
        error = "Tts error: non-cirilic symbols were detected - "
        return error
    except BaseException as e:
        return
        
def get_pcm(text, mtype):
    if(mtype == "text"):
        audio= model.apply_tts(text=text,
                                 speaker=speaker,
                                 sample_rate=sample_rate)
    elif(mtype == "ssml"):
        audio= model.apply_tts(ssml_text=text,
                                 speaker=speaker,
                                 sample_rate=sample_rate)
    audio = (audio * 32767).numpy().astype('int16')
    audio = bytes(audio)
    return audio

def get_wav(text, mtype):
    if(mtype == "text"): 
        audio_path= model.save_wav(text=text,
                                 speaker=speaker,
                                 sample_rate=sample_rate)
    elif(mtype == "ssml"):
        audio_path= model.save_wav(ssml_text=text,
                                 speaker=speaker,
                                 sample_rate=sample_rate)
    file = open(audio_path, "rb")
    audio = file.read()
    file.close()
    return audio


if(__name__ == "__main__"):
    with ThreadPoolExecutor() as e:
        while True:
            q = int(input())
            for i in range(0, q):
                e.submit(tts, "привет", "text", "pcm")