PGDMP  %    &                |            telegram_deploy_bot    16.3    16.3     �           0    0    ENCODING    ENCODING        SET client_encoding = 'UTF8';
                      false            �           0    0 
   STDSTRINGS 
   STDSTRINGS     (   SET standard_conforming_strings = 'on';
                      false            �           0    0 
   SEARCHPATH 
   SEARCHPATH     8   SELECT pg_catalog.set_config('search_path', '', false);
                      false            �           1262    17097    telegram_deploy_bot    DATABASE     �   CREATE DATABASE telegram_deploy_bot WITH TEMPLATE = template0 ENCODING = 'UTF8' LOCALE_PROVIDER = libc LOCALE = 'English_United States.1252';
 #   DROP DATABASE telegram_deploy_bot;
                postgres    false            �            1259    17148    scheduled_jobs    TABLE     �   CREATE TABLE public.scheduled_jobs (
    job_name character varying(255) NOT NULL,
    scheduled_time timestamp without time zone NOT NULL,
    created_at timestamp without time zone NOT NULL
);
 "   DROP TABLE public.scheduled_jobs;
       public         heap    postgres    false            �            1259    17184    user_feedback    TABLE     �   CREATE TABLE public.user_feedback (
    id integer NOT NULL,
    user_id bigint NOT NULL,
    user_name text,
    feedback_text text NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL
);
 !   DROP TABLE public.user_feedback;
       public         heap    postgres    false            �            1259    17183    user_feedback_id_seq    SEQUENCE     �   CREATE SEQUENCE public.user_feedback_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;
 +   DROP SEQUENCE public.user_feedback_id_seq;
       public          postgres    false    217            �           0    0    user_feedback_id_seq    SEQUENCE OWNED BY     M   ALTER SEQUENCE public.user_feedback_id_seq OWNED BY public.user_feedback.id;
          public          postgres    false    216                       2604    17187    user_feedback id    DEFAULT     t   ALTER TABLE ONLY public.user_feedback ALTER COLUMN id SET DEFAULT nextval('public.user_feedback_id_seq'::regclass);
 ?   ALTER TABLE public.user_feedback ALTER COLUMN id DROP DEFAULT;
       public          postgres    false    216    217    217            �          0    17148    scheduled_jobs 
   TABLE DATA           N   COPY public.scheduled_jobs (job_name, scheduled_time, created_at) FROM stdin;
    public          postgres    false    215   <       �          0    17184    user_feedback 
   TABLE DATA           Z   COPY public.user_feedback (id, user_id, user_name, feedback_text, created_at) FROM stdin;
    public          postgres    false    217   �       �           0    0    user_feedback_id_seq    SEQUENCE SET     B   SELECT pg_catalog.setval('public.user_feedback_id_seq', 3, true);
          public          postgres    false    216            !           2606    17152 "   scheduled_jobs scheduled_jobs_pkey 
   CONSTRAINT     f   ALTER TABLE ONLY public.scheduled_jobs
    ADD CONSTRAINT scheduled_jobs_pkey PRIMARY KEY (job_name);
 L   ALTER TABLE ONLY public.scheduled_jobs DROP CONSTRAINT scheduled_jobs_pkey;
       public            postgres    false    215            #           2606    17192     user_feedback user_feedback_pkey 
   CONSTRAINT     ^   ALTER TABLE ONLY public.user_feedback
    ADD CONSTRAINT user_feedback_pkey PRIMARY KEY (id);
 J   ALTER TABLE ONLY public.user_feedback DROP CONSTRAINT user_feedback_pkey;
       public            postgres    false    217            �   d   x�]̻�0 �:�" ��mi�T��I�]�=t�P���ۧ�1����}[�X�T'�'��R1'�?��K)'!��k�8n�QpBe��!K�n^ '3�!)      �   �   x�3�4437�4���4���K)M��00�t�3��K@��ᮥ%
����q��X��+�[[�����qm`������3�42�2�n�~AQ~VjrI1B����H���������W� 6G7�     