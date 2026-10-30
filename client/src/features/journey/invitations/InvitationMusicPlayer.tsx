import { useState } from "react";
import { invitationAudioSrc, invitationMusicSource } from "./invitationMusic";

export function InvitationMusicPlayer({ value, english = false }: { value?: string | null; english?: boolean }) {
  const source = invitationMusicSource(value);
  if (source.kind === "none") return null;
  if (source.kind === "invalid") return <p role="status" className="font-sans text-xs opacity-75">{english ? "Music is unavailable. You can still enjoy the invitation." : "Nhạc chưa khả dụng. Bạn vẫn có thể xem thiệp và gửi RSVP bình thường."}</p>;
  return <MusicPlayer key={source.url} source={source} english={english} />;
}

function MusicPlayer({ source, english }: {
  source: Exclude<ReturnType<typeof invitationMusicSource>, { kind: "none" | "invalid" }>;
  english: boolean;
}) {
  const [opened, setOpened] = useState(false);
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);
  return <section aria-label={english ? "Wedding music" : "Nhạc nền thiệp cưới"} className="font-sans rounded-2xl border border-current/15 p-4 space-y-3 min-w-0">
    <h3 className="text-sm font-semibold">{english ? "A song for our day" : "Giai điệu ngày chung đôi"}</h3>
    {source.kind === "youtube" ? <>
      {opened ? <iframe
        title={english ? "Wedding music on YouTube" : "Nhạc cưới trên YouTube"}
        className="w-full rounded-xl border-0 aspect-video min-h-[200px]"
        src={`https://www.youtube-nocookie.com/embed/${source.videoId}?autoplay=0&playsinline=1`}
        allow="encrypted-media; picture-in-picture; fullscreen"
        allowFullScreen referrerPolicy="strict-origin-when-cross-origin"
        onError={() => setFailed(true)}
      /> : <button type="button" className="rounded-xl border border-current/25 px-4 py-3 text-sm w-full" onClick={() => setOpened(true)}>{english ? "Load the YouTube player" : "Mở trình phát YouTube"}</button>}
      <p className="text-xs opacity-75">{english ? "YouTube only loads when you choose to open it. Press Play in the player; music never starts automatically." : "Chỉ kết nối YouTube khi bạn bấm mở. Bấm Play trong trình phát để nghe; nhạc không tự chạy."}</p>
      <a href={source.url} target="_blank" rel="noopener noreferrer" className="text-xs underline inline-block">{english ? "If the video is unavailable, open it on YouTube" : "Video không phát được? Mở trực tiếp trên YouTube"}</a>
    </> : <>
      <audio key={attempt} controls preload="none" className="w-full" src={invitationAudioSrc(source.url)} onError={() => setFailed(true)} onCanPlay={() => setFailed(false)}>
        {english ? "Your browser does not support audio." : "Trình duyệt không hỗ trợ phát nhạc."}
      </audio>
      <p className="text-xs opacity-75">{english ? "Press Play to listen. Use the player to pause or adjust volume." : "Bấm Play để nghe; dùng trình phát để tạm dừng hoặc chỉnh âm lượng."}</p>
      {failed && <button type="button" className="text-xs underline" onClick={() => { setFailed(false); setAttempt(n => n + 1); }}>{english ? "Try loading music again" : "Thử tải nhạc lại"}</button>}
    </>}
    {failed && <p role="status" className="text-xs">{english ? "The music source is unavailable. The rest of the invitation still works." : "Nguồn nhạc chưa khả dụng. Nội dung thiệp và RSVP vẫn hoạt động bình thường."}</p>}
  </section>;
}
