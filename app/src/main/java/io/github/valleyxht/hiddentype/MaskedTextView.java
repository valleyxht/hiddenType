package io.github.valleyxht.hiddentype;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.text.Layout;
import android.util.AttributeSet;

import androidx.annotation.Nullable;
import androidx.appcompat.widget.AppCompatTextView;

/**
 * Draws a subtle privacy texture behind the masked portion of the real text.
 * The text itself stays untouched, so wrapping, selection and saved content do
 * not change.
 */
public class MaskedTextView extends AppCompatTextView {
    private static final int VEIL_ALPHA = 31;   // About 12%.
    private static final int STRIPE_ALPHA = 61; // About 24%.

    private final Paint veilPaint = new Paint();
    private final Paint stripePaint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Path maskPath = new Path();
    private final RectF maskBounds = new RectF();
    private final float stripeGap;
    private int maskedCharacterEnd;

    public MaskedTextView(Context context) {
        this(context, null);
    }

    public MaskedTextView(Context context, @Nullable AttributeSet attrs) {
        this(context, attrs, android.R.attr.textViewStyle);
    }

    public MaskedTextView(Context context, @Nullable AttributeSet attrs, int defStyleAttr) {
        super(context, attrs, defStyleAttr);
        float density = getResources().getDisplayMetrics().density;
        stripeGap = 4f * density;

        int maskColor = context.getColor(R.color.mask_block_color);
        veilPaint.setStyle(Paint.Style.FILL);
        veilPaint.setColor(maskColor);
        veilPaint.setAlpha(VEIL_ALPHA);

        stripePaint.setStyle(Paint.Style.STROKE);
        stripePaint.setStrokeWidth(Math.max(1f, 0.75f * density));
        stripePaint.setColor(maskColor);
        stripePaint.setAlpha(STRIPE_ALPHA);
    }

    public void setMaskedCharacterEnd(int characterEnd) {
        int normalizedEnd = Math.max(0, characterEnd);
        if (maskedCharacterEnd == normalizedEnd) return;
        maskedCharacterEnd = normalizedEnd;
        invalidate();
    }

    @Override
    protected void onDraw(Canvas canvas) {
        drawMaskTexture(canvas);
        super.onDraw(canvas);
    }

    private void drawMaskTexture(Canvas canvas) {
        Layout layout = getLayout();
        CharSequence text = getText();
        int end = Math.min(maskedCharacterEnd, text == null ? 0 : text.length());
        if (layout == null || end <= 0) return;

        maskPath.reset();
        layout.getSelectionPath(0, end, maskPath);
        maskPath.computeBounds(maskBounds, true);
        if (maskBounds.isEmpty()) return;

        int translated = canvas.save();
        canvas.translate(getTotalPaddingLeft() - getScrollX(),
                getTotalPaddingTop() - getScrollY());
        canvas.drawPath(maskPath, veilPaint);

        int clipped = canvas.save();
        canvas.clipPath(maskPath);
        float height = maskBounds.height();
        for (float x = maskBounds.left - height; x < maskBounds.right + height; x += stripeGap) {
            canvas.drawLine(x, maskBounds.bottom, x + height, maskBounds.top, stripePaint);
        }
        canvas.restoreToCount(clipped);
        canvas.restoreToCount(translated);
    }
}
