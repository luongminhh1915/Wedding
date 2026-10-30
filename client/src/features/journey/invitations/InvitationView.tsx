import { useEffect, useState, type ReactNode } from "react";
import { CalendarDays, MapPin, Heart } from "lucide-react";
import {
  parseInvitationContent,
  type InvitationTemplate,
} from "./invitationApi";
import { invitationImageSrc } from "./invitationImages";
import { InvitationMusicPlayer } from "./InvitationMusicPlayer";

export interface InvitationDisplay {
  groomName: string;
  brideName: string;
  eventDate: string;
  venueName: string;
  venueAddress: string;
  coverImageUrl?: string;
  mapUrl?: string;
  loveStory?: string;
  bankInfo?: string;
  musicUrl?: string;
  lang?: string;
  contentJson?: string;
  templateStyle?: string;
}
export function InvitationView({
  invitation: i,
  template,
  children,
  compact = false,
}: {
  invitation: InvitationDisplay;
  template?: InvitationTemplate;
  children?: ReactNode;
  compact?: boolean;
}) {
  const c = parseInvitationContent(i.contentJson);
  const en = i.lang === "en";
  const date = new Date(i.eventDate);
  const primary = template?.primaryColor ?? "#233a42";
  const paper = template?.backgroundColor ?? "#fcf6ea";
  const dark = i.templateStyle === "midnight";
  const text = dark ? "#fcf6ea" : "#233a42";
  const panel = dark ? "#2b3b55" : "#ffffffb8";
  return (
    <article
      className={`mx-auto w-full ${compact ? "max-w-md" : "max-w-2xl"} overflow-hidden shadow-xl`}
      style={{
        background: paper,
        color: text,
        fontFamily:
          c.font === "Inter"
            ? "Inter, sans-serif"
            : "'Playfair Display', Georgia, serif",
      }}
    >
      <header
        className="text-center px-6 pt-12 pb-8 md:px-10"
        style={{ borderBottom: `1px solid ${primary}30` }}
      >
        <span
          aria-hidden
          className="text-5xl block mb-5"
          style={{ color: primary }}
        >
          {template?.motif ?? "∞"}
        </span>
        <p className="text-[10px] tracking-[.3em] uppercase mb-4">
          {en ? "Together with our families" : "Trân trọng kính mời"}
        </p>
        <h2
          className={`${compact ? "text-3xl" : "text-4xl md:text-5xl"} leading-snug`}
          style={{ color: primary }}
        >
          {i.groomName || (en ? "Groom" : "Chú rể")}
          <span className="block text-2xl italic my-1">&</span>
          {i.brideName || (en ? "Bride" : "Cô dâu")}
        </h2>
        <p className="text-sm mt-5 leading-relaxed">
          {c.welcome ||
            (en
              ? "Please join us to celebrate our wedding day."
              : "Hạnh phúc của chúng mình trọn vẹn hơn khi có bạn chung vui.")}
        </p>
        {Number.isFinite(date.getTime()) && (
          <p className="tracking-[.18em] mt-6 text-sm font-sans">
            {date.toLocaleDateString(en ? "en-GB" : "vi-VN", {
              day: "2-digit",
              month: "long",
              year: "numeric",
              timeZone: "Asia/Ho_Chi_Minh",
            })}
          </p>
        )}
        {Number.isFinite(date.getTime()) && (
          <Countdown eventDate={i.eventDate} english={en} />
        )}
      </header>
      {i.coverImageUrl && (
        <img
          src={invitationImageSrc(i.coverImageUrl)}
          alt={en ? "Our wedding photograph" : "Ảnh cưới của hai bạn"}
          className={`w-full object-cover ${i.templateStyle === "modern-arch" ? "rounded-t-full" : ""}`}
          style={{ maxHeight: compact ? 300 : 520, aspectRatio: "4/3" }}
        />
      )}
      <div className="p-6 md:p-9 space-y-6">
        {c.sections?.story !== false && i.loveStory && (
          <section className="text-center px-2 py-3">
            <Heart
              className="mx-auto mb-3"
              size={20}
              style={{ color: primary }}
            />
            <h3 className="text-xl mb-3">
              {en ? "Our story" : "Chuyện chúng mình"}
            </h3>
            <p className="text-sm whitespace-pre-wrap leading-7 opacity-85">
              {i.loveStory}
            </p>
          </section>
        )}
        <section
          className="text-center rounded-3xl p-6 space-y-3"
          style={{ background: panel, border: `1px solid ${primary}25` }}
        >
          <CalendarDays
            className="mx-auto"
            size={22}
            style={{ color: primary }}
          />
          <h3 className="text-2xl">
            {en ? "Wedding celebration" : "Lễ thành hôn & tiệc cưới"}
          </h3>
          {Number.isFinite(date.getTime()) && (
            <p className="font-sans text-sm">
              {date.toLocaleString(en ? "en-GB" : "vi-VN", {
                hour: "2-digit",
                minute: "2-digit",
                day: "2-digit",
                month: "2-digit",
                year: "numeric",
                timeZone: "Asia/Ho_Chi_Minh",
              })}{" "}
              <small>(GMT+7)</small>
            </p>
          )}
          <p className="font-bold">
            {i.venueName || (en ? "Wedding venue" : "Địa điểm tổ chức")}
          </p>
          <p className="text-sm opacity-80">{i.venueAddress}</p>
          {i.mapUrl && (
            <a
              className="inline-flex items-center gap-2 px-4 py-2 rounded-full font-sans text-xs"
              style={{ background: primary, color: dark ? "#233a42" : "#fff" }}
              href={i.mapUrl}
              target="_blank"
              rel="noreferrer"
            >
              <MapPin size={14} />
              {en ? "Directions" : "Xem chỉ đường"}
            </a>
          )}
        </section>
        {c.events?.map((event, n) => (
          <section
            key={n}
            className="text-center p-5 rounded-2xl"
            style={{ background: panel }}
          >
            <h3 className="text-xl">{event.name}</h3>
            <p className="font-sans text-xs mt-2">
              {event.date && Number.isFinite(new Date(event.date).getTime())
                ? new Date(event.date).toLocaleString(en ? "en-GB" : "vi-VN", {
                    timeZone: "Asia/Ho_Chi_Minh",
                  })
                : ""}
            </p>
            <p className="mt-2">{event.venue}</p>
            <p className="text-sm opacity-75">{event.address}</p>
          </section>
        ))}
        {c.dressCode && (
          <section className="text-center">
            <h3 className="text-xl mb-2">Dress code</h3>
            <p className="text-sm whitespace-pre-wrap">{c.dressCode}</p>
          </section>
        )}
        {c.sections?.gallery !== false && !!c.album?.length && (
          <section>
            <h3 className="text-xl text-center mb-4">
              {en ? "Our moments" : "Khoảnh khắc của hai bạn"}
            </h3>
            <div className="grid grid-cols-2 gap-3">
              {c.album.map((url, n) => (
                <img
                  key={`${url}-${n}`}
                  src={invitationImageSrc(url)}
                  loading="lazy"
                  alt={`${en ? "Wedding album" : "Album cưới"} ${n + 1}`}
                  className="w-full aspect-square object-cover rounded-2xl"
                />
              ))}
            </div>
          </section>
        )}
        <InvitationMusicPlayer value={i.musicUrl} english={en} />
        {i.bankInfo && (
          <section
            className="rounded-2xl p-5 text-center"
            style={{ background: panel }}
          >
            <h3 className="text-xl mb-3">
              {en ? "Wedding gift" : "Hộp mừng cưới"}
            </h3>
            <p className="font-sans text-sm whitespace-pre-wrap break-words">
              {i.bankInfo}
            </p>
          </section>
        )}
        {children}
      </div>
      <footer className="text-center pb-8 px-6 text-xs opacity-65 font-sans">
        {en
          ? "With love, created on Wedding Match"
          : "Gửi yêu thương · Được tạo trên Wedding Match"}
      </footer>
    </article>
  );
}

function Countdown({
  eventDate,
  english,
}: {
  eventDate: string;
  english: boolean;
}) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);
  const seconds = Math.max(
    0,
    Math.floor((new Date(eventDate).getTime() - now) / 1000),
  );
  const values = [
    Math.floor(seconds / 86400),
    Math.floor(seconds / 3600) % 24,
    Math.floor(seconds / 60) % 60,
    seconds % 60,
  ];
  const names = english
    ? ["Days", "Hours", "Minutes", "Seconds"]
    : ["Ngày", "Giờ", "Phút", "Giây"];
  return (
    <div
      className="flex justify-center gap-4 mt-5 font-sans"
      aria-label={english ? "Wedding countdown" : "Đếm ngược ngày cưới"}
    >
      {values.map((value, n) => (
        <span key={names[n]}>
          <strong className="block text-xl">
            {String(value).padStart(2, "0")}
          </strong>
          <small className="text-[9px] uppercase tracking-wider opacity-65">
            {names[n]}
          </small>
        </span>
      ))}
    </div>
  );
}
