import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { Heart, CheckCircle2, Loader2, Send } from "lucide-react";
import { usePublicInvitation, useSubmitRsvp } from "../../hooks/useInvitations";
import { InvitationView } from "../../../journey/invitations/InvitationView";
import {
  useInvitationTemplates,
  parseInvitationContent,
} from "../../../journey/invitations/invitationApi";
import type { SubmitRsvpRequest } from "../../../../types/invitation.types";
import { invitationImageSrc } from "../../../journey/invitations/invitationImages";

export default function PublicInvitationPage() {
  const { slug = "" } = useParams<{ slug: string }>();
  const query = usePublicInvitation(slug);
  const templates = useInvitationTemplates();
  const submit = useSubmitRsvp(slug);
  const i = query.data;
  const en = i?.lang === "en";
  const content = parseInvitationContent(i?.contentJson);
  const [f, setF] = useState<SubmitRsvpRequest>({
    guestName: "",
    phoneNumber: "",
    status: "Attending",
    companionCount: 0,
    wishes: "",
    dietaryPreference: "",
    type: "Attend",
  });
  const [key] = useState(() => crypto.randomUUID());
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  useEffect(() => {
    if (!i) return;
    const before = document.title;
    document.title =
      content.ogTitle || `${i.groomName} & ${i.brideName} | Wedding Match`;
    const created: HTMLMetaElement[] = [];
    for (const [property, value] of [
      ["og:title", document.title],
      [
        "og:description",
        content.ogDescription || i.loveStory || "Wedding Match",
      ],
      [
        "og:image",
        i.coverImageUrl
          ? new URL(invitationImageSrc(i.coverImageUrl), window.location.origin)
              .href
          : "",
      ],
    ]) {
      const tag = document.createElement("meta");
      tag.setAttribute("property", property);
      tag.content = value;
      document.head.appendChild(tag);
      created.push(tag);
    }
    return () => {
      document.title = before;
      created.forEach((tag) => tag.remove());
    };
  }, [i, content.ogTitle, content.ogDescription]);
  const send = async () => {
    if (!f.guestName.trim()) {
      setError(en ? "Please enter your name." : "Vui lòng nhập tên của bạn.");
      return;
    }
    setError("");
    try {
      const r = await submit.mutateAsync({ ...f, submissionKey: key });
      setMessage(en ? "Thank you! Your response has been saved." : r.message);
    } catch (e) {
      setError((e as Error).message);
    }
  };
  if (query.isLoading)
    return (
      <main className="min-h-screen flex flex-col justify-center items-center bg-[#f2efe4]">
        <Loader2 className="animate-spin" />
        <p className="mt-3 text-sm">Đang mở thiệp cưới...</p>
      </main>
    );
  if (query.isError || !i)
    return (
      <main className="min-h-screen flex flex-col justify-center items-center text-center p-6 bg-[#f2efe4] text-[#25334c]">
        <Heart size={40} />
        <h1 className="text-2xl font-bold my-4">Thiệp chưa được chia sẻ</h1>
        <p>
          {query.error?.message ||
            "Liên kết không tồn tại hoặc thiệp đang được tạm ẩn."}
        </p>
        <Link to="/" className="mt-5 underline">
          Về Wedding Match
        </Link>
      </main>
    );
  const template = templates.data?.find((t) => t.key === i.templateStyle);
  return (
    <main className="min-h-screen py-0 md:py-10 bg-[#e9e6db]">
      <InvitationView template={template} invitation={i}>
        <section
          className="bg-white text-[#25334c] rounded-3xl p-5 md:p-7 font-sans"
          aria-label="RSVP"
        >
          <h3 className="text-2xl text-center font-serif mb-2">
            {en ? "Will you join us?" : "Bạn sẽ đến chung vui chứ?"}
          </h3>
          <p className="text-xs text-center text-[#53637b] mb-5">
            {en
              ? "Your presence means the world to us."
              : "Sự hiện diện của bạn là niềm vui của chúng mình."}
          </p>
          {message ? (
            <div className="text-center py-5">
              <CheckCircle2 className="mx-auto text-green-700 mb-3" size={36} />
              <p role="status">{message}</p>
            </div>
          ) : (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                void send();
              }}
              className="space-y-4"
            >
              {error && (
                <p className="text-red-700 text-sm" role="alert">
                  {error}
                </p>
              )}
              <label className="block text-sm">
                {en ? "Your name *" : "Tên của bạn *"}
                <input
                  required
                  maxLength={100}
                  value={f.guestName}
                  onChange={(e) =>
                    setF((x) => ({ ...x, guestName: e.target.value }))
                  }
                  className="block w-full border border-[#dedccf] rounded-xl px-3 py-2 mt-1"
                />
              </label>
              <label className="block text-sm">
                {en ? "Phone (optional)" : "Điện thoại (không bắt buộc)"}
                <input
                  maxLength={30}
                  type="tel"
                  value={f.phoneNumber}
                  onChange={(e) =>
                    setF((x) => ({ ...x, phoneNumber: e.target.value }))
                  }
                  className="block w-full border border-[#dedccf] rounded-xl px-3 py-2 mt-1"
                />
              </label>
              <label className="block text-sm">
                {en ? "Response" : "Phản hồi"}
                <select
                  value={f.type === "Wish" ? "Wish" : f.status}
                  onChange={(e) =>
                    setF((x) => ({
                      ...x,
                      type: e.target.value === "Wish" ? "Wish" : "Attend",
                      status:
                        e.target.value === "Wish"
                          ? "Tentative"
                          : (e.target.value as SubmitRsvpRequest["status"]),
                    }))
                  }
                  className="block w-full border border-[#dedccf] rounded-xl px-3 py-2 mt-1"
                >
                  <option value="Attending">
                    {en ? "Happily attending" : "Mình sẽ tham dự"}
                  </option>
                  <option value="NotAttending">
                    {en
                      ? "Sorry, unable to attend"
                      : "Tiếc quá, mình không tham dự được"}
                  </option>
                  <option value="Tentative">
                    {en ? "Not sure yet" : "Mình chưa chắc"}
                  </option>
                  <option value="Wish">
                    {en ? "Send wishes only" : "Chỉ gửi lời chúc"}
                  </option>
                </select>
              </label>
              {f.type !== "Wish" && f.status !== "NotAttending" && (
                <div className="grid sm:grid-cols-2 gap-3">
                  <label className="block text-sm">
                    {en ? "Additional guests" : "Người đi cùng"}
                    <input
                      type="number"
                      min={0}
                      max={19}
                      value={f.companionCount}
                      onChange={(e) =>
                        setF((x) => ({ ...x, companionCount: +e.target.value }))
                      }
                      className="block w-full border border-[#dedccf] rounded-xl px-3 py-2 mt-1"
                    />
                  </label>
                  <label className="block text-sm">
                    {en ? "Dietary requirements" : "Ăn uống / dị ứng"}
                    <input
                      maxLength={500}
                      value={f.dietaryPreference}
                      onChange={(e) =>
                        setF((x) => ({
                          ...x,
                          dietaryPreference: e.target.value,
                        }))
                      }
                      className="block w-full border border-[#dedccf] rounded-xl px-3 py-2 mt-1"
                    />
                  </label>
                </div>
              )}
              <label className="block text-sm">
                {en ? "Your wishes" : "Lời chúc của bạn"}
                <textarea
                  maxLength={2000}
                  rows={3}
                  value={f.wishes}
                  onChange={(e) =>
                    setF((x) => ({ ...x, wishes: e.target.value }))
                  }
                  className="block w-full border border-[#dedccf] rounded-xl px-3 py-2 mt-1"
                />
              </label>
              <button
                disabled={submit.isPending}
                className="w-full rounded-xl py-3 bg-[#25334c] text-[#f2efe4] font-bold text-sm flex items-center justify-center gap-2 disabled:opacity-60"
              >
                <Send size={14} />
                {submit.isPending
                  ? en
                    ? "Sending..."
                    : "Đang gửi..."
                  : en
                    ? "Send response"
                    : "Gửi phản hồi"}
              </button>
              <p className="text-[10px] text-[#53637b]">
                {en
                  ? "Your phone number is only visible to the couple and their authorized planning members. Your name and wishes may appear publicly below."
                  : "Điện thoại chỉ hiển thị với cặp đôi và thành viên được cấp quyền. Tên và lời chúc có thể xuất hiện công khai trong sổ lưu bút."}
              </p>
            </form>
          )}
        </section>
        {content.sections?.wishes !== false && i.recentWishes.length > 0 && (
          <section className="rounded-3xl p-5 bg-white text-[#25334c]">
            <h3 className="text-xl text-center mb-4">
              {en ? "Guestbook" : "Sổ lưu bút"}
            </h3>
            <ul className="space-y-4 max-h-96 overflow-y-auto">
              {i.recentWishes.map((w, n) => (
                <li
                  key={`${w.createdAt}-${n}`}
                  className="border-b border-[#dedccf] pb-3"
                >
                  <strong className="text-sm">{w.guestName}</strong>
                  <p className="text-sm italic whitespace-pre-wrap mt-1">
                    {w.wishes}
                  </p>
                </li>
              ))}
            </ul>
          </section>
        )}
      </InvitationView>
    </main>
  );
}
