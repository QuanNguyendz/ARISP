export class AudioUtils {
  static async createAudioContext(): Promise<AudioContext> {
    return new AudioContext();
  }

  static async getUserMedia(): Promise<MediaStream> {
    return navigator.mediaDevices.getUserMedia({
      audio: {
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true,
      },
    });
  }

  static setAudioStreamVolume(stream: MediaStream, volume: number): void {
    const audioTracks = stream.getAudioTracks();
    audioTracks.forEach((track) => {
      const settings = track.getSettings();
      if (settings.volume !== undefined) {
        Object.defineProperty(track, 'volume', {
          get: () => volume,
          set: (v: number) => {
            const gainNode = new GainNode();
            gainNode.gain.value = v;
          },
        });
      }
    });
  }

  static async playAudio(audioBuffer: ArrayBuffer): Promise<void> {
    const audioContext = new AudioContext();
    const audioSource = audioContext.decodeAudioData(audioBuffer.slice(0));
    const buffer = await audioSource;
    const source = audioContext.createBufferSource();
    source.buffer = buffer;
    source.connect(audioContext.destination);
    source.start();
  }
}
