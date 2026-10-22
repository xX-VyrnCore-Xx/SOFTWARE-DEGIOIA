using System.Windows;
using System.Windows.Markup;

namespace PresenzeXmlConverter.Wpf;

using TextBlock = System.Windows.Controls.TextBlock;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Cursors = System.Windows.Input.Cursors;
using DragDropEffects = System.Windows.DragDropEffects;
using Button = System.Windows.Controls.Button;
using Control = System.Windows.Controls.Control;
using Label = System.Windows.Controls.Label;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using Window = System.Windows.Window;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Orientation = System.Windows.Controls.Orientation;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Brush = System.Windows.Media.Brush;
using Application = System.Windows.Application;

/// <summary>
/// Gli stili e i modelli (template) di tutti i controlli della nuova interfaccia, scritti come XAML e
/// composti con i colori della tavolozza attiva. Pulsanti, campi, interruttori, barre di scorrimento,
/// suggerimenti, menù e tabelle hanno angoli morbidi, passaggi animati (circa 140 ms) e nessun
/// colore di sistema: quello che si vede è sempre ciò che la tavolozza dice.
/// </summary>
internal static class UiStyles
{
    public const string FontStack = "Segoe UI Variable Text, Segoe UI, Arial";

    /// <summary>Il dizionario di risorse da unire a quelli dell'applicazione.</summary>
    public static ResourceDictionary Create(UiPalette p) => (ResourceDictionary)XamlReader.Parse(Xaml(p));

    /// <summary>Il testo XAML del dizionario (separato per poterlo ispezionare nei test).</summary>
    internal static string Xaml(UiPalette p) => $$$"""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Colori come pennelli nominati (usati anche dal codice) -->
  <SolidColorBrush x:Key="Brush.Ink" Color="{{{p.Ink}}}"/>
  <SolidColorBrush x:Key="Brush.InkSecondary" Color="{{{p.InkSecondary}}}"/>
  <SolidColorBrush x:Key="Brush.Muted" Color="{{{p.Muted}}}"/>
  <SolidColorBrush x:Key="Brush.Faint" Color="{{{p.Faint}}}"/>
  <SolidColorBrush x:Key="Brush.Surface" Color="{{{p.Surface}}}"/>
  <SolidColorBrush x:Key="Brush.Canvas" Color="{{{p.Canvas}}}"/>
  <SolidColorBrush x:Key="Brush.Sunken" Color="{{{p.Sunken}}}"/>
  <SolidColorBrush x:Key="Brush.Sidebar" Color="{{{p.Sidebar}}}"/>
  <SolidColorBrush x:Key="Brush.SidebarHover" Color="{{{p.SidebarHover}}}"/>
  <SolidColorBrush x:Key="Brush.Divider" Color="{{{p.Divider}}}"/>
  <SolidColorBrush x:Key="Brush.DividerStrong" Color="{{{p.DividerStrong}}}"/>
  <SolidColorBrush x:Key="Brush.Brand" Color="{{{p.Brand}}}"/>
  <SolidColorBrush x:Key="Brush.BrandSoft" Color="{{{p.BrandSoft}}}"/>
  <SolidColorBrush x:Key="Brush.BrandInk" Color="{{{p.BrandInk}}}"/>
  <SolidColorBrush x:Key="Brush.Success" Color="{{{p.Success}}}"/>
  <SolidColorBrush x:Key="Brush.SuccessSoft" Color="{{{p.SuccessSoft}}}"/>
  <SolidColorBrush x:Key="Brush.SuccessInk" Color="{{{p.SuccessInk}}}"/>
  <SolidColorBrush x:Key="Brush.Warning" Color="{{{p.Warning}}}"/>
  <SolidColorBrush x:Key="Brush.WarningSoft" Color="{{{p.WarningSoft}}}"/>
  <SolidColorBrush x:Key="Brush.WarningInk" Color="{{{p.WarningInk}}}"/>
  <SolidColorBrush x:Key="Brush.Danger" Color="{{{p.Danger}}}"/>
  <SolidColorBrush x:Key="Brush.DangerSoft" Color="{{{p.DangerSoft}}}"/>
  <SolidColorBrush x:Key="Brush.DangerInk" Color="{{{p.DangerInk}}}"/>

  <DropShadowEffect x:Key="Shadow.Card" BlurRadius="22" ShadowDepth="3" Direction="270" Opacity="0.07" Color="#000000"/>
  <DropShadowEffect x:Key="Shadow.Popup" BlurRadius="30" ShadowDepth="8" Direction="270" Opacity="0.18" Color="#000000"/>

  <!-- Testo -->
  <Style TargetType="TextBlock">
    <Setter Property="FontFamily" Value="{{{UiStyles.FontStack}}}"/>
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="TextTrimming" Value="None"/>
  </Style>

  <!-- Card -->
  <Style x:Key="Card" TargetType="Border">
    <Setter Property="Background" Value="{{{p.Surface}}}"/>
    <Setter Property="BorderBrush" Value="{{{p.Divider}}}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="14"/>
  </Style>

  <!-- Pulsante: secondario (predefinito) -->
  <Style TargetType="Button">
    <Setter Property="FontFamily" Value="{{{UiStyles.FontStack}}}"/>
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Foreground" Value="{{{p.Ink}}}"/>
    <Setter Property="Background" Value="{{{p.Surface}}}"/>
    <Setter Property="BorderBrush" Value="{{{p.NeutralBorder}}}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="16,0"/>
    <Setter Property="MinHeight" Value="36"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Grid>
            <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="10"/>
            <Border x:Name="Hover" Background="{{{p.HoverVeil}}}" CornerRadius="10" Opacity="0" IsHitTestVisible="False"/>
            <Border x:Name="Press" Background="{{{p.PressVeil}}}" CornerRadius="10" Opacity="0" IsHitTestVisible="False"/>
            <Border x:Name="Ring" BorderBrush="{{{p.Brand}}}" BorderThickness="2" CornerRadius="10" Opacity="0" IsHitTestVisible="False"/>
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="Center" VerticalAlignment="Center"
                              RecognizesAccessKey="True" TextElement.Foreground="{TemplateBinding Foreground}"/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Trigger.EnterActions><BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName="Hover" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.14"/>
              </Storyboard></BeginStoryboard></Trigger.EnterActions>
              <Trigger.ExitActions><BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName="Hover" Storyboard.TargetProperty="Opacity" To="0" Duration="0:0:0.22"/>
              </Storyboard></BeginStoryboard></Trigger.ExitActions>
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Trigger.EnterActions><BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName="Press" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.06"/>
              </Storyboard></BeginStoryboard></Trigger.EnterActions>
              <Trigger.ExitActions><BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName="Press" Storyboard.TargetProperty="Opacity" To="0" Duration="0:0:0.16"/>
              </Storyboard></BeginStoryboard></Trigger.ExitActions>
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Ring" Property="Opacity" Value="0.55"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.45"/>
              <Setter Property="Cursor" Value="Arrow"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Pulsante principale (blu) -->
  <Style x:Key="Primary" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="Foreground" Value="#FFFFFF"/>
    <Setter Property="Background" Value="{{{p.Brand}}}"/>
    <Setter Property="BorderBrush" Value="{{{p.Brand}}}"/>
  </Style>

  <!-- Pulsante senza cornice -->
  <Style x:Key="Ghost" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="Transparent"/>
    <Setter Property="Foreground" Value="{{{p.InkSecondary}}}"/>
  </Style>

  <!-- Pulsante di pericolo -->
  <Style x:Key="Danger" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="Foreground" Value="#FFFFFF"/>
    <Setter Property="Background" Value="{{{p.Danger}}}"/>
    <Setter Property="BorderBrush" Value="{{{p.Danger}}}"/>
  </Style>

  <!-- Campo di testo -->
  <Style TargetType="TextBox">
    <Setter Property="FontFamily" Value="{{{UiStyles.FontStack}}}"/>
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="Foreground" Value="{{{p.Ink}}}"/>
    <Setter Property="Background" Value="{{{p.Field}}}"/>
    <Setter Property="BorderBrush" Value="{{{p.NeutralBorder}}}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="12,0"/>
    <Setter Property="MinHeight" Value="38"/>
    <Setter Property="CaretBrush" Value="{{{p.Brand}}}"/>
    <Setter Property="SelectionBrush" Value="{{{p.BrandLite}}}"/>
    <Setter Property="VerticalContentAlignment" Value="Center"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TextBox">
          <Grid>
            <Border x:Name="Ring" BorderBrush="{{{p.BrandLite}}}" BorderThickness="3" CornerRadius="12" Margin="-2" Opacity="0" IsHitTestVisible="False"/>
            <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="10">
              <Grid>
                <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" VerticalAlignment="Center"
                              Focusable="False" HorizontalScrollBarVisibility="Hidden" VerticalScrollBarVisibility="Hidden"/>
                <TextBlock x:Name="Hint" Text="{TemplateBinding Tag}" Margin="{TemplateBinding Padding}" VerticalAlignment="Center"
                           Foreground="{{{p.Faint}}}" IsHitTestVisible="False" Visibility="Collapsed"/>
              </Grid>
            </Border>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="Text" Value="">
              <Setter TargetName="Hint" Property="Visibility" Value="Visible"/>
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Bd" Property="BorderBrush" Value="{{{p.Brand}}}"/>
              <Trigger.EnterActions><BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName="Ring" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.14"/>
              </Storyboard></BeginStoryboard></Trigger.EnterActions>
              <Trigger.ExitActions><BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName="Ring" Storyboard.TargetProperty="Opacity" To="0" Duration="0:0:0.2"/>
              </Storyboard></BeginStoryboard></Trigger.ExitActions>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.5"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Campo del token (testo nascosto) -->
  <Style TargetType="PasswordBox">
    <Setter Property="FontFamily" Value="{{{UiStyles.FontStack}}}"/>
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="Foreground" Value="{{{p.Ink}}}"/>
    <Setter Property="Background" Value="{{{p.Field}}}"/>
    <Setter Property="BorderBrush" Value="{{{p.NeutralBorder}}}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="12,0"/>
    <Setter Property="MinHeight" Value="38"/>
    <Setter Property="CaretBrush" Value="{{{p.Brand}}}"/>
    <Setter Property="SelectionBrush" Value="{{{p.BrandLite}}}"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="PasswordBox">
          <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="10">
            <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" VerticalAlignment="Center" Focusable="False"
                          HorizontalScrollBarVisibility="Hidden" VerticalScrollBarVisibility="Hidden"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Bd" Property="BorderBrush" Value="{{{p.Brand}}}"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.5"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Elenco a discesa -->
  <Style x:Key="ComboToggle" TargetType="ToggleButton">
    <Setter Property="OverridesDefaultStyle" Value="True"/>
    <Setter Property="IsTabStop" Value="False"/>
    <Setter Property="Focusable" Value="False"/>
    <Setter Property="ClickMode" Value="Press"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ToggleButton">
          <Border x:Name="Bd" Background="{{{p.Field}}}" BorderBrush="{{{p.NeutralBorder}}}" BorderThickness="1" CornerRadius="10">
            <Path HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,14,0" Data="M 0 0 L 5 5 L 10 0"
                  Stroke="{{{p.Muted}}}" StrokeThickness="1.8" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="Bd" Property="BorderBrush" Value="{{{p.Brand}}}"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="ComboBoxItem">
    <Setter Property="FontFamily" Value="{{{UiStyles.FontStack}}}"/>
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="Foreground" Value="{{{p.Ink}}}"/>
    <Setter Property="Padding" Value="12,8"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ComboBoxItem">
          <Border x:Name="Bd" Background="Transparent" CornerRadius="8" Margin="4,1" Padding="{TemplateBinding Padding}">
            <ContentPresenter TextElement.Foreground="{TemplateBinding Foreground}"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsHighlighted" Value="True">
              <Setter TargetName="Bd" Property="Background" Value="{{{p.BrandSoft}}}"/>
            </Trigger>
            <Trigger Property="IsSelected" Value="True">
              <Setter TargetName="Bd" Property="Background" Value="{{{p.BrandSoft}}}"/>
              <Setter Property="Foreground" Value="{{{p.BrandInk}}}"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="ComboBox">
    <Setter Property="FontFamily" Value="{{{UiStyles.FontStack}}}"/>
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="Foreground" Value="{{{p.Ink}}}"/>
    <Setter Property="MinHeight" Value="38"/>
    <Setter Property="Padding" Value="12,0,34,0"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Disabled"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ComboBox">
          <Grid>
            <ToggleButton Style="{StaticResource ComboToggle}" IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"/>
            <ContentPresenter Margin="{TemplateBinding Padding}" VerticalAlignment="Center" HorizontalAlignment="Left" IsHitTestVisible="False"
                              Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                              TextElement.Foreground="{TemplateBinding Foreground}"/>
            <Popup x:Name="PART_Popup" Placement="Bottom" IsOpen="{TemplateBinding IsDropDownOpen}" AllowsTransparency="True" Focusable="False"
                   PopupAnimation="Fade" VerticalOffset="4">
              <Border MinWidth="{TemplateBinding ActualWidth}" MaxHeight="{TemplateBinding MaxDropDownHeight}" Background="{{{p.Surface}}}"
                      BorderBrush="{{{p.Divider}}}" BorderThickness="1" CornerRadius="10" Padding="0,4" Margin="14" Effect="{StaticResource Shadow.Popup}">
                <ScrollViewer>
                  <ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.5"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Interruttore (una CheckBox con la grafica di un interruttore) -->
  <Style x:Key="Toggle" TargetType="CheckBox">
    <Setter Property="FontFamily" Value="{{{UiStyles.FontStack}}}"/>
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="Foreground" Value="{{{p.InkSecondary}}}"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="CheckBox">
          <Grid Background="Transparent" MinHeight="40">
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="*"/>
              <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <ContentPresenter Grid.Column="0" VerticalAlignment="Center" Margin="0,6,16,6" RecognizesAccessKey="True"
                              TextElement.Foreground="{TemplateBinding Foreground}"/>
            <Border Grid.Column="1" Width="46" Height="28" CornerRadius="14" VerticalAlignment="Center">
              <Border.Background>
                <SolidColorBrush x:Name="Track" Color="{{{p.DividerStrong}}}"/>
              </Border.Background>
              <Border x:Name="Thumb" Width="24" Height="24" CornerRadius="12" Background="#FFFFFF" HorizontalAlignment="Left"
                      Margin="2,0,0,0">
                <Border.RenderTransform><TranslateTransform x:Name="Slide" X="0"/></Border.RenderTransform>
                <Border.Effect><DropShadowEffect BlurRadius="4" ShadowDepth="1" Opacity="0.28" Color="#000000"/></Border.Effect>
              </Border>
            </Border>
            <Border x:Name="Ring" Grid.Column="1" Width="52" Height="34" CornerRadius="17" BorderBrush="{{{p.BrandLite}}}"
                    BorderThickness="3" Opacity="0" IsHitTestVisible="False"/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsChecked" Value="True">
              <Trigger.EnterActions><BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName="Slide" Storyboard.TargetProperty="X" To="18" Duration="0:0:0.18">
                  <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
                </DoubleAnimation>
                <ColorAnimation Storyboard.TargetName="Track" Storyboard.TargetProperty="Color" To="{{{p.Success}}}" Duration="0:0:0.18"/>
              </Storyboard></BeginStoryboard></Trigger.EnterActions>
              <Trigger.ExitActions><BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName="Slide" Storyboard.TargetProperty="X" To="0" Duration="0:0:0.18">
                  <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
                </DoubleAnimation>
                <ColorAnimation Storyboard.TargetName="Track" Storyboard.TargetProperty="Color" To="{{{p.DividerStrong}}}" Duration="0:0:0.18"/>
              </Storyboard></BeginStoryboard></Trigger.ExitActions>
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Ring" Property="Opacity" Value="1"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.5"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Barre di scorrimento sottili -->
  <Style x:Key="ScrollTrackButton" TargetType="RepeatButton">
    <Setter Property="Focusable" Value="False"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style x:Key="ScrollThumb" TargetType="Thumb">
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Thumb">
          <Border x:Name="Bar" Background="{{{p.ThumbRest}}}" CornerRadius="4" Margin="2"/>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Bar" Property="Background" Value="{{{p.ThumbHover}}}"/>
            </Trigger>
            <Trigger Property="IsDragging" Value="True">
              <Setter TargetName="Bar" Property="Background" Value="{{{p.ThumbDrag}}}"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <ControlTemplate x:Key="VerticalScrollBar" TargetType="ScrollBar">
    <Grid Background="Transparent" Width="12">
      <Track x:Name="PART_Track" IsDirectionReversed="True">
        <Track.DecreaseRepeatButton><RepeatButton Style="{StaticResource ScrollTrackButton}" Command="ScrollBar.PageUpCommand"/></Track.DecreaseRepeatButton>
        <Track.Thumb><Thumb Style="{StaticResource ScrollThumb}"/></Track.Thumb>
        <Track.IncreaseRepeatButton><RepeatButton Style="{StaticResource ScrollTrackButton}" Command="ScrollBar.PageDownCommand"/></Track.IncreaseRepeatButton>
      </Track>
    </Grid>
  </ControlTemplate>
  <ControlTemplate x:Key="HorizontalScrollBar" TargetType="ScrollBar">
    <Grid Background="Transparent" Height="12">
      <Track x:Name="PART_Track">
        <Track.DecreaseRepeatButton><RepeatButton Style="{StaticResource ScrollTrackButton}" Command="ScrollBar.PageLeftCommand"/></Track.DecreaseRepeatButton>
        <Track.Thumb><Thumb Style="{StaticResource ScrollThumb}"/></Track.Thumb>
        <Track.IncreaseRepeatButton><RepeatButton Style="{StaticResource ScrollTrackButton}" Command="ScrollBar.PageRightCommand"/></Track.IncreaseRepeatButton>
      </Track>
    </Grid>
  </ControlTemplate>
  <Style TargetType="ScrollBar">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Template" Value="{StaticResource VerticalScrollBar}"/>
    <Style.Triggers>
      <Trigger Property="Orientation" Value="Horizontal">
        <Setter Property="Template" Value="{StaticResource HorizontalScrollBar}"/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <!-- Tabelle -->
  <Style TargetType="DataGridColumnHeader">
    <Setter Property="Foreground" Value="{{{p.Muted}}}"/>
    <Setter Property="FontSize" Value="12.5"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Height" Value="38"/>
    <Setter Property="HorizontalContentAlignment" Value="Left"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="DataGridColumnHeader">
          <Border Background="{{{p.Sunken}}}" Padding="12,0">
            <ContentPresenter VerticalAlignment="Center" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                              TextElement.Foreground="{TemplateBinding Foreground}"/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType="DataGridCell">
    <Setter Property="Foreground" Value="{{{p.Ink}}}"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="DataGridCell">
          <Border Background="Transparent" Padding="12,0" SnapsToDevicePixels="True">
            <ContentPresenter VerticalAlignment="Center"/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType="DataGridRow">
    <Setter Property="Background" Value="{{{p.Surface}}}"/>
    <Setter Property="MinHeight" Value="38"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="ValidationErrorTemplate" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="DataGridRow">
          <Border Background="{TemplateBinding Background}" SnapsToDevicePixels="True">
            <SelectiveScrollingGrid>
              <SelectiveScrollingGrid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
              </SelectiveScrollingGrid.ColumnDefinitions>
              <DataGridCellsPresenter Grid.Column="1" ItemsPanel="{TemplateBinding ItemsPanel}"
                                      SnapsToDevicePixels="{TemplateBinding SnapsToDevicePixels}"/>
            </SelectiveScrollingGrid>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
    <Style.Triggers>
      <Trigger Property="AlternationIndex" Value="1">
        <Setter Property="Background" Value="{{{p.AltRow}}}"/>
      </Trigger>
      <Trigger Property="IsMouseOver" Value="True">
        <Setter Property="Background" Value="{{{p.HoverRow}}}"/>
      </Trigger>
      <Trigger Property="IsSelected" Value="True">
        <Setter Property="Background" Value="{{{p.BrandLite}}}"/>
      </Trigger>
    </Style.Triggers>
  </Style>
  <Style TargetType="DataGrid">
    <Setter Property="Background" Value="{{{p.Surface}}}"/>
    <Setter Property="Foreground" Value="{{{p.Ink}}}"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="FontFamily" Value="{{{UiStyles.FontStack}}}"/>
    <Setter Property="FontSize" Value="13.5"/>
    <Setter Property="GridLinesVisibility" Value="Horizontal"/>
    <Setter Property="HorizontalGridLinesBrush" Value="{{{p.Divider}}}"/>
    <Setter Property="HeadersVisibility" Value="Column"/>
    <Setter Property="RowHeaderWidth" Value="0"/>
    <Setter Property="AutoGenerateColumns" Value="False"/>
    <Setter Property="CanUserAddRows" Value="False"/>
    <Setter Property="CanUserDeleteRows" Value="False"/>
    <Setter Property="CanUserResizeRows" Value="False"/>
    <Setter Property="IsReadOnly" Value="True"/>
    <Setter Property="SelectionMode" Value="Single"/>
    <Setter Property="SelectionUnit" Value="FullRow"/>
    <Setter Property="AlternationCount" Value="2"/>
    <Setter Property="EnableRowVirtualization" Value="True"/>
    <Setter Property="ColumnHeaderHeight" Value="38"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
  </Style>

  <!-- Suggerimenti -->
  <Style TargetType="ToolTip">
    <Setter Property="HasDropShadow" Value="False"/>
    <Setter Property="Placement" Value="Bottom"/>
    <Setter Property="VerticalOffset" Value="6"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ToolTip">
          <Border Background="{{{p.Ink}}}" CornerRadius="8" Padding="11,7" MaxWidth="320">
            <ContentPresenter TextElement.Foreground="{{{p.Surface}}}" TextElement.FontSize="12.5"
                              TextElement.FontFamily="{{{UiStyles.FontStack}}}"/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

</ResourceDictionary>
""";
}
